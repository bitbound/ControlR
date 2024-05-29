using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ControlR.Shared.Dtos.GitHubDtos;

public class Release
{
    public string? Url { get; set; }
    public string? AssetsUrl { get; set; }
    public string? UploadUrl { get; set; }
    public string? HtmlUrl { get; set; }
    public int Id { get; set; }
    public Author? Author { get; set; }
    public string? NodeId { get; set; }
    public string? TagName { get; set; }
    public string? TargetCommitish { get; set; }
    public string? Name { get; set; }
    public bool Draft { get; set; }
    public bool Prerelease { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime PublishedAt { get; set; }
    public Asset[] Assets { get; set; } = [];
    public string? TarballUrl { get; set; }
    public string? ZipballUrl { get; set; }
    public string? Body { get; set; }
}

