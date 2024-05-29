using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlR.Shared.Dtos.GitHubDtos;

public class Asset
{
    public string? Url { get; set; }
    public int Id { get; set; }
    public string? NodeId { get; set; }
    public string? Name { get; set; }
    public string? Label { get; set; }
    public Uploader? Uploader { get; set; }
    public string? ContentType { get; set; }
    public string? State { get; set; }
    public int Size { get; set; }
    public int DownloadCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? BrowserDownloadUrl { get; set; }
}