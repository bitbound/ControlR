using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services.FileSystem;

namespace ControlR.Libraries.TestingUtilities.FileSystem;

public class FakeFileSystem(char directorySeparator = '/', bool isCaseSensitive = false) : IFileSystem
{
	private readonly Dictionary<string, FakeDirectoryEntry> _directories = new(GetComparer(isCaseSensitive));
	private readonly Dictionary<string, FakeDriveEntry> _drives = new(GetComparer(isCaseSensitive));
	private readonly Dictionary<string, FakeFileEntry> _files = new(GetComparer(isCaseSensitive));
	private readonly Dictionary<string, List<FakeFileHandle>> _openHandles = new(GetComparer(isCaseSensitive));
	private readonly Dictionary<string, string> _resolvedFilePaths = new(GetComparer(isCaseSensitive));
	private readonly Lock _syncRoot = new();
	private readonly Dictionary<string, string> _versionInfoSources = new(GetComparer(isCaseSensitive));

	public void AddDirectory(string directoryPath, FileAttributes attributes = FileAttributes.Directory, DateTime? lastWriteTime = null)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(directoryPath);
			EnsureDirectoryHierarchy(normalizedPath, attributes, lastWriteTime ?? DateTime.UtcNow);
		}
	}

	public void AddDrive(
		string rootPath,
		string? name = null,
		DriveType driveType = DriveType.Fixed,
		string driveFormat = "memfs",
		long totalSize = 0,
		long totalFreeSpace = 0,
		string volumeLabel = "",
		bool isReady = true)
	{
		lock (_syncRoot)
		{
			var normalizedRoot = NormalizePath(rootPath);
			EnsureDirectoryHierarchy(normalizedRoot);
			_drives[normalizedRoot] = new FakeDriveEntry(
				name ?? normalizedRoot,
				normalizedRoot,
				driveType,
				driveFormat,
				totalSize,
				totalFreeSpace,
				volumeLabel,
				isReady);
		}
	}

	public void AddFile(
		string filePath,
		byte[] content,
		FileAttributes attributes = FileAttributes.Normal,
		DateTime? lastWriteTime = null)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(filePath);
			EnsureParentDirectoryExists(normalizedPath, createIfMissing: true);
			_files[normalizedPath] = new FakeFileEntry(normalizedPath, content.ToArray())
			{
				Attributes = attributes,
				LastWriteTime = lastWriteTime ?? DateTime.UtcNow
			};
			TouchParent(normalizedPath);
		}
	}

	public void AddFile(
		string filePath,
		string content,
		FileAttributes attributes = FileAttributes.Normal,
		DateTime? lastWriteTime = null)
	{
		AddFile(filePath, Encoding.UTF8.GetBytes(content), attributes, lastWriteTime);
	}

	public Task AppendAllLinesAsync(string path, IEnumerable<string> lines)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(path);
			var existingLines = _files.TryGetValue(normalizedPath, out var existingFile)
				? ReadLines(existingFile.Content)
				: [];
			var updatedLines = existingLines.Concat(lines).ToList();
			WriteLinesInternal(normalizedPath, updatedLines);
		}

		return Task.CompletedTask;
	}

	public void CopyFile(string sourceFile, string destinationFile, bool overwrite)
	{
		lock (_syncRoot)
		{
			var sourcePath = NormalizePath(sourceFile);
			var destinationPath = NormalizePath(destinationFile);
			var source = GetExistingFile(sourcePath);

			if (_directories.ContainsKey(destinationPath))
			{
				throw new IOException($"Destination path '{destinationFile}' is a directory.");
			}

			EnsureParentDirectoryExists(destinationPath);

			if (_files.ContainsKey(destinationPath) && !overwrite)
			{
				throw new IOException($"The file '{destinationFile}' already exists.");
			}

			_files[destinationPath] = source.Clone(destinationPath);
			TouchParent(destinationPath);
		}
	}

	public IFileSystemDirectory CreateDirectory(string directoryPath)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(directoryPath);
			EnsureDirectoryHierarchy(normalizedPath);
			return ToDirectoryInfo(normalizedPath);
		}
	}

	public Stream CreateFile(string filePath)
	{
		return OpenFileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
	}

	public Stream CreateFileStream(string filePath, FileMode mode)
	{
		var access = mode == FileMode.Append ? FileAccess.Write : FileAccess.ReadWrite;
		return OpenFileStream(filePath, mode, access, FileShare.None);
	}

	public Stream CreateFileStream(string filePath, FileMode mode, FileAccess access, FileShare fileShare)
	{
		return OpenFileStream(filePath, mode, access, fileShare);
	}

	public void DeleteDirectory(string directoryPath, bool recursive)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(directoryPath);
			if (!_directories.ContainsKey(normalizedPath))
			{
				throw new DirectoryNotFoundException($"Could not find a part of the path '{directoryPath}'.");
			}

			var hasChildren = _directories.Keys.Any(x => IsDirectChild(normalizedPath, x)) ||
				_files.Keys.Any(x => IsDirectChild(normalizedPath, x));

			if (hasChildren && !recursive)
			{
				throw new IOException($"The directory '{directoryPath}' is not empty.");
			}

			foreach (var childFile in _files.Keys.Where(x => IsDescendantOrSelf(normalizedPath, x)).ToArray())
			{
				_files.Remove(childFile);
			}

			foreach (var childDirectory in _directories.Keys.Where(x => x != normalizedPath && IsDescendantOrSelf(normalizedPath, x)).ToArray())
			{
				_directories.Remove(childDirectory);
			}

			_directories.Remove(normalizedPath);
			TouchParent(normalizedPath);
		}
	}

	public void DeleteFile(string filePath)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(filePath);
			_ = _files.Remove(normalizedPath);

			TouchParent(normalizedPath);
		}
	}

	public bool DirectoryExists(string directoryPath)
	{
		lock (_syncRoot)
		{
			return _directories.ContainsKey(NormalizePath(directoryPath));
		}
	}

	public void ExtractZipArchive(string sourceArchiveFileName, string destinationDirectoryName, bool overwriteFiles)
	{
		lock (_syncRoot)
		{
			var sourceArchivePath = NormalizePath(sourceArchiveFileName);
			var archiveFile = GetExistingFile(sourceArchivePath);
			var destinationRoot = NormalizePath(destinationDirectoryName);
			EnsureDirectoryHierarchy(destinationRoot);

			using var archiveStream = new MemoryStream(archiveFile.Content, writable: false);
			using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);

			foreach (var entry in archive.Entries)
			{
				var destinationPath = CombineNormalized(destinationRoot, entry.FullName.Replace('/', directorySeparator));
				if (string.IsNullOrEmpty(entry.Name))
				{
					EnsureDirectoryHierarchy(destinationPath);
					continue;
				}

				EnsureParentDirectoryExists(destinationPath, createIfMissing: true);
				if (_files.ContainsKey(destinationPath) && !overwriteFiles)
				{
					throw new IOException($"The file '{destinationPath}' already exists.");
				}

				using var entryStream = entry.Open();
				using var memoryStream = new MemoryStream();
				entryStream.CopyTo(memoryStream);
				_files[destinationPath] = new FakeFileEntry(destinationPath, memoryStream.ToArray())
				{
					Attributes = GetFileAttributesFromExternalAttributes(entry.ExternalAttributes),
					UnixFileMode = GetUnixFileModeFromExternalAttributes(entry.ExternalAttributes)
				};
			}
		}
	}

	public bool FileExists(string path)
	{
		lock (_syncRoot)
		{
			return _files.ContainsKey(NormalizePath(path));
		}
	}

	public string[] GetDirectories(string path)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(path);
			EnsureDirectoryExists(normalizedPath, path);

			return _directories.Keys
				.Where(x => IsDirectChild(normalizedPath, x))
				.OrderBy(x => x, isCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}
	}

	public IFileSystemDirectory GetDirectoryInfo(string directoryPath)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(directoryPath);
			return ToDirectoryInfo(normalizedPath, _directories.TryGetValue(normalizedPath, out var directory)
				? directory
				: null);
		}
	}

	public IFileSystemDrive[] GetDrives()
	{
		lock (_syncRoot)
		{
			return [.. _drives.Values.Select(ToDriveInfo)];
		}
	}

	public IFileSystemFile GetFileInfo(string filePath)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(filePath);
			return _files.TryGetValue(normalizedPath, out var fileEntry)
				? ToFileInfo(fileEntry)
				: new FakeFileSystemFileInfo(normalizedPath)
				{
					Name = GetName(normalizedPath)
				};
		}
	}

	public string[] GetFiles(string path)
	{
		return GetFiles(path, "*", SearchOption.TopDirectoryOnly);
	}

	public string[] GetFiles(string path, string searchPattern)
	{
		return GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
	}

	public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(path);
			EnsureDirectoryExists(normalizedPath, path);
			var regex = CreateSearchPatternRegex(searchPattern);
			var includeSubdirectories = searchOption == SearchOption.AllDirectories;

			return _files.Keys
				.Where(x => includeSubdirectories
					? IsDescendant(normalizedPath, x)
					: IsDirectChild(normalizedPath, x))
				.Where(x => regex.IsMatch(Path.GetFileName(x)))
				.OrderBy(x => x, isCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}
	}

	public string[] GetFiles(string path, string searchPattern, EnumerationOptions enumerationOptions)
	{
		var searchOption = enumerationOptions.RecurseSubdirectories
			? SearchOption.AllDirectories
			: SearchOption.TopDirectoryOnly;

		return GetFiles(path, searchPattern, searchOption);
	}

	public FileVersionInfo GetFileVersionInfo(string filePath)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(filePath);
			_ = GetExistingFile(normalizedPath);
			var sourcePath = _versionInfoSources.TryGetValue(normalizedPath, out var configuredSource)
				? configuredSource
				: typeof(FakeFileSystem).Assembly.Location;

			return FileVersionInfo.GetVersionInfo(sourcePath);
		}
	}

	public string JoinPaths(char separator, params string[] paths)
	{
		var builder = new StringBuilder();

		for (var i = 0; i < paths.Length; i++)
		{
			var path = paths[i];
			if (string.IsNullOrEmpty(path))
			{
				continue;
			}

			if (builder.Length == 0)
			{
				builder.Append(path);
				continue;
			}

			if (builder[^1] != separator && path[0] != separator)
			{
				builder.Append(separator);
			}

			builder.Append(path);
		}

		return builder.ToString();
	}

	public void MoveFile(string sourceFile, string destinationFile, bool overwrite)
	{
		lock (_syncRoot)
		{
			var sourcePath = NormalizePath(sourceFile);
			var destinationPath = NormalizePath(destinationFile);
			var source = GetExistingFile(sourcePath);

			if (_files.ContainsKey(destinationPath) && !overwrite)
			{
				throw new IOException($"The file '{destinationFile}' already exists.");
			}

			EnsureParentDirectoryExists(destinationPath);
			_files[destinationPath] = source.Clone(destinationPath);
			_files.Remove(sourcePath);
			TouchParent(sourcePath);
			TouchParent(destinationPath);
		}
	}

	public Stream OpenFileStream(string path, FileMode mode, FileAccess access)
	{
		return OpenFileStream(path, mode, access, FileShare.None);
	}

	public Stream OpenFileStream(string path, FileMode mode, FileAccess access, FileShare fileShare)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(path);
			var append = mode == FileMode.Append;
			ValidateOpenArguments(mode, access);

			if (_directories.ContainsKey(normalizedPath))
			{
				throw new UnauthorizedAccessException($"Access to the path '{path}' is denied.");
			}

			var canWrite = access is FileAccess.Write or FileAccess.ReadWrite;
			EnsureFileShareCompatibility(normalizedPath, access, fileShare);

			FakeFileEntry fileEntry;
			switch (mode)
			{
				case FileMode.CreateNew:
					if (_files.ContainsKey(normalizedPath))
					{
						throw new IOException($"The file '{path}' already exists.");
					}

					fileEntry = CreateEmptyFile(normalizedPath);
					break;
				case FileMode.Create:
					fileEntry = CreateEmptyFile(normalizedPath);
					break;
				case FileMode.Open:
					fileEntry = GetExistingFile(normalizedPath);
					break;
				case FileMode.OpenOrCreate:
					fileEntry = _files.TryGetValue(normalizedPath, out var existingFile)
						? existingFile
						: CreateEmptyFile(normalizedPath);
					break;
				case FileMode.Truncate:
					fileEntry = GetExistingFile(normalizedPath);
					if (!canWrite)
					{
						throw new ArgumentException("Truncate requires write access.", nameof(access));
					}

					fileEntry = CreateEmptyFile(normalizedPath);
					break;
				case FileMode.Append:
					fileEntry = _files.TryGetValue(normalizedPath, out var appendFile)
						? appendFile
						: CreateEmptyFile(normalizedPath);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
			}

			var handle = RegisterOpenHandle(normalizedPath, access, fileShare);

			return new FakeFileSystemStream(
				fileEntry.Content,
				append,
				RequestsRead(access),
				RequestsWrite(access),
				content => CommitFile(normalizedPath, content),
				() => ReleaseOpenHandle(normalizedPath, handle));
		}
	}

	public Task<byte[]> ReadAllBytesAsync(string path)
	{
		lock (_syncRoot)
		{
			return Task.FromResult(GetExistingFile(NormalizePath(path)).Content.ToArray());
		}
	}

	public Task<string[]> ReadAllLinesAsync(string path)
	{
		lock (_syncRoot)
		{
			return Task.FromResult(ReadLines(GetExistingFile(NormalizePath(path)).Content));
		}
	}

	public string ReadAllText(string filePath)
	{
		lock (_syncRoot)
		{
			return Encoding.UTF8.GetString(GetExistingFile(NormalizePath(filePath)).Content);
		}
	}

	public Task<string> ReadAllTextAsync(string path)
	{
		lock (_syncRoot)
		{
			return Task.FromResult(Encoding.UTF8.GetString(GetExistingFile(NormalizePath(path)).Content));
		}
	}

	public Task ReplaceLineInFile(string filePath, string matchPattern, string replaceLineWith, int maxMatches = -1)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(filePath);
			var lines = ReadLines(GetExistingFile(normalizedPath).Content);
			var matchCount = 0;
			for (var i = 0; i < lines.Length; i++)
			{
				if (lines[i].Contains(matchPattern, StringComparison.OrdinalIgnoreCase))
				{
					lines[i] = replaceLineWith;
					matchCount++;
				}

				if (maxMatches > -1 && matchCount >= maxMatches)
				{
					break;
				}
			}

			WriteLinesInternal(normalizedPath, lines);
		}

		return Task.CompletedTask;
	}

	public Task<Result<string>> ResolveFilePath(string fileName)
	{
		lock (_syncRoot)
		{
			if (_resolvedFilePaths.TryGetValue(fileName, out var resolvedPath))
			{
				return Task.FromResult(Result.Ok(resolvedPath));
			}

			return Task.FromResult(Result.Fail<string>($"File '{fileName}' not found."));
		}
	}

	public void SetResolvedFilePath(string fileName, string resolvedPath)
	{
		lock (_syncRoot)
		{
			_resolvedFilePaths[fileName] = NormalizePath(resolvedPath);
		}
	}

	public void SetUnixFileMode(string filePath, UnixFileMode fileMode)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(filePath);
			if (_files.TryGetValue(normalizedPath, out var file))
			{
				file.UnixFileMode = fileMode;
				return;
			}

			if (_directories.TryGetValue(normalizedPath, out var directory))
			{
				directory.UnixFileMode = fileMode;
				return;
			}

			throw new FileNotFoundException($"Could not find file '{filePath}'.", filePath);
		}
	}

	public void SetVersionInfoSource(string filePath, string sourceFilePath)
	{
		lock (_syncRoot)
		{
			_versionInfoSources[NormalizePath(filePath)] = sourceFilePath;
		}
	}

	public Task WriteAllBytesAsync(string path, byte[] buffer, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(path);
			EnsureParentDirectoryExists(normalizedPath, createIfMissing: true);
			_files.TryGetValue(normalizedPath, out var existingFile);
			_files[normalizedPath] = new FakeFileEntry(normalizedPath, buffer.ToArray())
			{
				Attributes = existingFile?.Attributes ?? FileAttributes.Normal,
				LastWriteTime = DateTime.UtcNow,
				UnixFileMode = existingFile?.UnixFileMode
			};
			TouchParent(normalizedPath);
		}

		return Task.CompletedTask;
	}

	public Task WriteAllLines(string path, List<string> lines)
	{
		lock (_syncRoot)
		{
			WriteLinesInternal(NormalizePath(path), lines);
		}

		return Task.CompletedTask;
	}

	public void WriteAllText(string filePath, string contents)
	{
		lock (_syncRoot)
		{
			var normalizedPath = NormalizePath(filePath);
			EnsureParentDirectoryExists(normalizedPath, createIfMissing: true);
			_files.TryGetValue(normalizedPath, out var existingFile);
			_files[normalizedPath] = new FakeFileEntry(normalizedPath, Encoding.UTF8.GetBytes(contents))
			{
				Attributes = existingFile?.Attributes ?? FileAttributes.Normal,
				LastWriteTime = DateTime.UtcNow,
				UnixFileMode = existingFile?.UnixFileMode
			};
			TouchParent(normalizedPath);
		}
	}

	public Task WriteAllTextAsync(string path, string content)
	{
		WriteAllText(path, content);
		return Task.CompletedTask;
	}

	private static bool AllowsRead(FileShare fileShare)
	{
		return fileShare.HasFlag(FileShare.Read);
	}

	private static bool AllowsWrite(FileShare fileShare)
	{
		return fileShare.HasFlag(FileShare.Write);
	}

	private static StringComparer GetComparer(bool isCaseSensitive)
	{
		return isCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
	}

	private static FileAttributes GetFileAttributesFromExternalAttributes(int externalAttributes)
	{
		// For Unix files, the lower 16 bits contain the Unix file mode
		// For Windows files, the upper 16 bits contain the Windows file attributes
		// We need to extract the Windows file attributes (upper 16 bits)
		return (FileAttributes)(externalAttributes >> 16);
	}

	private static UnixFileMode GetUnixFileModeFromExternalAttributes(int externalAttributes)
	{
		// For Unix files, the lower 16 bits contain the Unix file mode
		return (UnixFileMode)(externalAttributes & 0xFFFF);
	}

	private static string[] ReadLines(byte[] content)
	{
    if (content.Length == 0)
		{
			return [];
		}

		var text = Encoding.UTF8.GetString(content);
		return text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
	}

	private static bool RequestsRead(FileAccess fileAccess)
	{
		return fileAccess is FileAccess.Read or FileAccess.ReadWrite;
	}

	private static bool RequestsWrite(FileAccess fileAccess)
	{
		return fileAccess is FileAccess.Write or FileAccess.ReadWrite;
	}

	private static void ValidateOpenArguments(FileMode mode, FileAccess access)
	{
		if (mode == FileMode.Append && access != FileAccess.Write)
		{
			throw new ArgumentException("Append access can be requested only in write-only mode.", nameof(access));
		}
	}

	private string AppendSeparator(string path)
	{
		return path.EndsWith(directorySeparator)
			? path
			: path + directorySeparator;
	}

	private string CombineNormalized(string left, string right)
	{
		return NormalizePath(JoinPaths(directorySeparator, left, right));
	}

	private void CommitFile(string normalizedPath, byte[] content)
	{
		lock (_syncRoot)
		{
			EnsureParentDirectoryExists(normalizedPath, createIfMissing: true);
			_files.TryGetValue(normalizedPath, out var existingFile);
			_files[normalizedPath] = new FakeFileEntry(normalizedPath, content.ToArray())
			{
				Attributes = existingFile?.Attributes ?? FileAttributes.Normal,
				LastWriteTime = DateTime.UtcNow,
				UnixFileMode = existingFile?.UnixFileMode
			};
			TouchParent(normalizedPath);
		}
	}

	private FakeFileEntry CreateEmptyFile(string normalizedPath)
	{
		EnsureParentDirectoryExists(normalizedPath, createIfMissing: true);
		var fileEntry = new FakeFileEntry(normalizedPath, []);
		_files[normalizedPath] = fileEntry;
		TouchParent(normalizedPath);
		return fileEntry;
	}

	private Regex CreateSearchPatternRegex(string searchPattern)
	{
		var escapedPattern = Regex.Escape(searchPattern)
			.Replace("\\*", ".*")
			.Replace("\\?", ".");

		var options = RegexOptions.Compiled;
		if (!isCaseSensitive)
		{
			options |= RegexOptions.IgnoreCase;
		}

		return new Regex($"^{escapedPattern}$", options);
	}

	private void EnsureDirectoryExists(string normalizedPath, string originalPath)
	{
		if (!_directories.ContainsKey(normalizedPath))
		{
			throw new DirectoryNotFoundException($"Could not find a part of the path '{originalPath}'.");
		}
	}

	private void EnsureDirectoryHierarchy(string normalizedPath, FileAttributes? attributes = null, DateTime? lastWriteTime = null)
	{
		var root = NormalizeRoot(normalizedPath);
		if (!string.IsNullOrEmpty(root) && !_directories.ContainsKey(root))
		{
			_directories[root] = new FakeDirectoryEntry(root)
			{
				LastWriteTime = lastWriteTime ?? DateTime.UtcNow
			};
		}

		var current = root;
		var remainder = normalizedPath[root.Length..].Trim(directorySeparator);
		if (string.IsNullOrEmpty(remainder))
		{
			if (_directories.TryGetValue(normalizedPath, out var rootDirectory))
			{
				rootDirectory.Attributes = attributes ?? rootDirectory.Attributes;
				rootDirectory.LastWriteTime = lastWriteTime ?? rootDirectory.LastWriteTime;
			}

			return;
		}

		foreach (var segment in remainder.Split(directorySeparator, StringSplitOptions.RemoveEmptyEntries))
		{
			current = string.IsNullOrEmpty(current)
				? segment
				: JoinPaths(directorySeparator, current, segment);

			if (!_directories.TryGetValue(current, out var directory))
			{
				_directories[current] = new FakeDirectoryEntry(current)
				{
					LastWriteTime = lastWriteTime ?? DateTime.UtcNow
				};
				continue;
			}

			directory.LastWriteTime = lastWriteTime ?? directory.LastWriteTime;
		}

		if (_directories.TryGetValue(normalizedPath, out var existingDirectory))
		{
			existingDirectory.Attributes = attributes ?? existingDirectory.Attributes;
			existingDirectory.LastWriteTime = lastWriteTime ?? existingDirectory.LastWriteTime;
		}
	}

	private void EnsureFileShareCompatibility(string normalizedPath, FileAccess access, FileShare fileShare)
	{
		if (!_openHandles.TryGetValue(normalizedPath, out var handles))
		{
			return;
		}

		foreach (var handle in handles)
		{
			if (RequestsRead(access) && !AllowsRead(handle.FileShare))
			{
				throw new IOException($"The process cannot access the file '{normalizedPath}' because it is being used by another process.");
			}

			if (RequestsWrite(access) && !AllowsWrite(handle.FileShare))
			{
				throw new IOException($"The process cannot access the file '{normalizedPath}' because it is being used by another process.");
			}

			if (RequestsRead(handle.FileAccess) && !AllowsRead(fileShare))
			{
				throw new IOException($"The process cannot access the file '{normalizedPath}' because it is being used by another process.");
			}

			if (RequestsWrite(handle.FileAccess) && !AllowsWrite(fileShare))
			{
				throw new IOException($"The process cannot access the file '{normalizedPath}' because it is being used by another process.");
			}
		}
	}

	private void EnsureParentDirectoryExists(string normalizedPath, bool createIfMissing = false)
	{
		var parentPath = GetParentPath(normalizedPath);
		if (string.IsNullOrEmpty(parentPath))
		{
			return;
		}

		if (_directories.ContainsKey(parentPath))
		{
			return;
		}

		if (createIfMissing)
		{
			EnsureDirectoryHierarchy(parentPath);
			return;
		}

		throw new DirectoryNotFoundException($"Could not find a part of the path '{normalizedPath}'.");
	}

	private FakeFileEntry GetExistingFile(string normalizedPath)
	{
		if (_files.TryGetValue(normalizedPath, out var fileEntry))
		{
			return fileEntry;
		}

		throw new FileNotFoundException($"Could not find file '{normalizedPath}'.", normalizedPath);
	}

	private string GetName(string normalizedPath)
	{
		var trimmedPath = normalizedPath.TrimEnd(directorySeparator);
		var root = NormalizeRoot(trimmedPath);
		if (trimmedPath == root)
		{
			return root;
		}

		var separatorIndex = trimmedPath.LastIndexOf(directorySeparator);
		return separatorIndex < 0 ? trimmedPath : trimmedPath[(separatorIndex + 1)..];
	}

	private string GetParentPath(string normalizedPath)
	{
		var root = NormalizeRoot(normalizedPath);
		if (normalizedPath == root)
		{
			return string.Empty;
		}

		var trimmedPath = normalizedPath.TrimEnd(directorySeparator);
		var separatorIndex = trimmedPath.LastIndexOf(directorySeparator);
		if (separatorIndex < root.Length)
		{
			return root;
		}

		return trimmedPath[..separatorIndex];
	}

	private bool IsDescendant(string parentPath, string childPath)
	{
		return childPath.StartsWith(
			AppendSeparator(parentPath),
			isCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) &&
			childPath != parentPath;
	}

	private bool IsDescendantOrSelf(string parentPath, string childPath)
	{
		return childPath == parentPath || IsDescendant(parentPath, childPath);
	}

	private bool IsDirectChild(string parentPath, string childPath)
	{
		if (!IsDescendant(parentPath, childPath))
		{
			return false;
		}

		var remainder = childPath[AppendSeparator(parentPath).Length..];
		return !remainder.Contains(directorySeparator);
	}

	private string NormalizePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}

		var normalized = path.Replace(directorySeparator == '/' ? '\\' : '/', directorySeparator).Trim();
		var root = NormalizeRoot(normalized);
		var remainder = normalized[root.Length..];
		var parts = remainder.Split(directorySeparator, StringSplitOptions.RemoveEmptyEntries);
		var stack = new List<string>();

		foreach (var part in parts)
		{
			if (part == ".")
			{
				continue;
			}

			if (part == "..")
			{
				if (stack.Count > 0)
				{
					stack.RemoveAt(stack.Count - 1);
				}

				continue;
			}

			stack.Add(part);
		}

		if (stack.Count == 0)
		{
			return root;
		}

		return string.IsNullOrEmpty(root)
			? string.Join(directorySeparator, stack)
			: root + string.Join(directorySeparator, stack);
	}

	private string NormalizeRoot(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}

		var normalized = path.Replace(directorySeparator == '/' ? '\\' : '/', directorySeparator);
		if (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':')
		{
			return normalized.Length >= 3 && normalized[2] == directorySeparator
				? normalized[..3]
				: $"{normalized[..2]}{directorySeparator}";
		}

		return normalized[0] == directorySeparator
			? directorySeparator.ToString()
			: string.Empty;
	}

	private FakeFileHandle RegisterOpenHandle(string normalizedPath, FileAccess fileAccess, FileShare fileShare)
	{
		if (!_openHandles.TryGetValue(normalizedPath, out var handles))
		{
			handles = [];
			_openHandles[normalizedPath] = handles;
		}

		var handle = new FakeFileHandle(fileAccess, fileShare);
		handles.Add(handle);
		return handle;
	}

	private void ReleaseOpenHandle(string normalizedPath, FakeFileHandle handle)
	{
		lock (_syncRoot)
		{
			if (!_openHandles.TryGetValue(normalizedPath, out var handles))
			{
				return;
			}

			_ = handles.Remove(handle);
			if (handles.Count == 0)
			{
				_openHandles.Remove(normalizedPath);
			}
		}
	}

	private IFileSystemDirectory ToDirectoryInfo(string normalizedPath, FakeDirectoryEntry? directory = null)
	{
		var parentPath = GetParentPath(normalizedPath);
		return new FakeFileSystemDirectoryInfo(normalizedPath)
		{
			Attributes = directory?.Attributes ?? FileAttributes.Directory,
			Exists = directory is not null,
			LastWriteTime = directory?.LastWriteTime ?? DateTime.MinValue,
			Name = GetName(normalizedPath),
			Parent = string.IsNullOrEmpty(parentPath) || parentPath == normalizedPath
				? null
				: ToDirectoryInfo(parentPath, _directories.TryGetValue(parentPath, out var parentDirectory)
					? parentDirectory
					: null)
		};
	}

	private IFileSystemDrive ToDriveInfo(FakeDriveEntry drive)
	{
		return new FakeFileSystemDriveInfo(drive.Name, drive.RootPath)
		{
			DriveFormat = drive.DriveFormat,
			DriveType = drive.DriveType,
			IsReady = drive.IsReady,
			RootDirectory = ToDirectoryInfo(drive.RootPath),
			TotalFreeSpace = drive.TotalFreeSpace,
			TotalSize = drive.TotalSize,
			VolumeLabel = drive.VolumeLabel
		};
	}

	private IFileSystemFile ToFileInfo(FakeFileEntry fileEntry)
	{
		return new FakeFileSystemFileInfo(fileEntry.FullName)
		{
			Attributes = fileEntry.Attributes,
			LastWriteTime = fileEntry.LastWriteTime,
			Length = fileEntry.Content.LongLength,
			Name = GetName(fileEntry.FullName)
		};
	}

	private void TouchParent(string normalizedPath)
	{
		var parentPath = GetParentPath(normalizedPath);
		if (!string.IsNullOrEmpty(parentPath) && _directories.TryGetValue(parentPath, out var parent))
		{
			parent.LastWriteTime = DateTime.UtcNow;
		}
	}

	private void WriteLinesInternal(string normalizedPath, IEnumerable<string> lines)
	{
		EnsureParentDirectoryExists(normalizedPath, createIfMissing: true);
		_files.TryGetValue(normalizedPath, out var existingFile);
		var content = string.Join(Environment.NewLine, lines);
		_files[normalizedPath] = new FakeFileEntry(normalizedPath, Encoding.UTF8.GetBytes(content))
		{
			Attributes = existingFile?.Attributes ?? FileAttributes.Normal,
			LastWriteTime = DateTime.UtcNow,
			UnixFileMode = existingFile?.UnixFileMode
		};
		TouchParent(normalizedPath);
	}

  private sealed class FakeDirectoryEntry(string fullName)
	{
		public FileAttributes Attributes { get; set; } = FileAttributes.Directory;

		public string FullName { get; } = fullName;

		public DateTime LastWriteTime { get; set; } = DateTime.UtcNow;

		public UnixFileMode? UnixFileMode { get; set; }
	}
	private sealed class FakeDriveEntry(
		string name,
		string rootPath,
		DriveType driveType,
		string driveFormat,
		long totalSize,
		long totalFreeSpace,
		string volumeLabel,
		bool isReady)
	{
		public string DriveFormat { get; } = driveFormat;

		public DriveType DriveType { get; } = driveType;

		public bool IsReady { get; } = isReady;

		public string Name { get; } = name;

		public string RootPath { get; } = rootPath;

		public long TotalFreeSpace { get; } = totalFreeSpace;

		public long TotalSize { get; } = totalSize;

		public string VolumeLabel { get; } = volumeLabel;
	}
	private sealed class FakeFileEntry(string fullName, byte[] content)
	{
		public FileAttributes Attributes { get; set; } = FileAttributes.Normal;

		public byte[] Content { get; set; } = content;

		public string FullName { get; } = fullName;

		public DateTime LastWriteTime { get; set; } = DateTime.UtcNow;

		public UnixFileMode? UnixFileMode { get; set; }

		public FakeFileEntry Clone(string destinationPath)
		{
			return new FakeFileEntry(destinationPath, Content.ToArray())
			{
				Attributes = Attributes,
				LastWriteTime = DateTime.UtcNow,
				UnixFileMode = UnixFileMode
			};
		}
	}
}