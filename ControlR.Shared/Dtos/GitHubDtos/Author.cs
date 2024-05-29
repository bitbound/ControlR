using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlR.Shared.Dtos.GitHubDtos;

public class Author
{
    public string? Login { get; set; }
    public int Id { get; set; }
    public string? NodeId { get; set; }
    public string? AvatarUrl { get; set; }
    public string? GravatarId { get; set; }
    public string? Url { get; set; }
    public string? HtmlUrl { get; set; }
    public string? FollowersUrl { get; set; }
    public string? FollowingUrl { get; set; }
    public string? GistsUrl { get; set; }
    public string? StarredUrl { get; set; }
    public string? SubscriptionsUrl { get; set; }
    public string? OrganizationsUrl { get; set; }
    public string? ReposUrl { get; set; }
    public string? EventsUrl { get; set; }
    public string? ReceivedEventsUrl { get; set; }
    public string? Type { get; set; }
    public bool SiteAdmin { get; set; }
}
