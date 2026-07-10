using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace apiship.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

    public List<Feature> Features { get; } = new()
    {
        new("⚡", "Blazing fast", "Global edge network delivers responses in under 40ms so your apps feel instant."),
        new("🔒", "Secure by default", "OAuth2, API keys and rate limiting built in to keep every endpoint protected."),
        new("📈", "Real-time analytics", "Track usage, errors and latency with dashboards updated in real time."),
        new("🧩", "Easy integration", "Clean REST endpoints and SDKs get you connected in just a few lines of code."),
        new("🔁", "Auto scaling", "Handle one request or a million — infrastructure scales automatically."),
        new("🛠️", "99.99% uptime", "Resilient, redundant architecture backed by an enterprise-grade SLA."),
        new("🌍", "Global edge", "Requests are served from the region closest to your users worldwide."),
        new("📚", "Great docs & SDKs", "Clear guides and client libraries for JavaScript, Python, Go and more.")
    };

    public List<Review> Reviews { get; } = new()
    {
        new("ApiShip cut our integration time from weeks to a single afternoon. The docs are fantastic.", "Sarah Lin", "CTO, Nimbus", "SL"),
        new("Reliable, fast and easy to monitor. Our team finally stopped worrying about API infrastructure.", "David Okoye", "Lead Dev, Payflow", "DO"),
        new("The analytics dashboard alone is worth it. We caught issues before customers ever noticed.", "Mia Torres", "Founder, Shiply", "MT")
    };

    public List<Plan> Plans { get; } = new()
    {
        new("Starter", 50, "For small projects getting off the ground.", new[]
        {
            "100k API calls / month",
            "Up to 3 endpoints",
            "Email support",
            "Basic analytics"
        }, false),
        new("Growth", 120, "For growing teams that need more power.", new[]
        {
            "2M API calls / month",
            "Unlimited endpoints",
            "Priority support",
            "Advanced analytics",
            "Custom rate limits"
        }, true),
        new("Scale", 300, "For high-traffic, business-critical APIs.", new[]
        {
            "20M API calls / month",
            "Unlimited endpoints",
            "24/7 dedicated support",
            "99.99% uptime SLA",
            "SSO & audit logs"
        }, false)
    };

    public void OnGet()
    {
    }
}

public record Feature(string Icon, string Title, string Description);

public record Review(string Quote, string Name, string Role, string Initials);

public record Plan(string Name, int Price, string Description, string[] Features, bool Featured);

