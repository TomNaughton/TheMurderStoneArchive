
# The Murder Stone Archive

This repository contains an ASP.NET Core web application that documents historical markers, folklore stones, and boundary markers.

Goals
- Provide a factual, encyclopedia-like presentation of sites (Wikipedia-inspired styling and structure).
- Allow community submissions with moderation (IsApproved flag).
- Present data on a map and detailed pages with infoboxes and references.

Development notes
- .NET Target: .NET 10
- Project type: ASP.NET Core MVC with Entity Framework Core and Identity (ApplicationDbContext).
- Useful endpoints:
  - / : homepage with interactive map
  - /MurderEvents : list of events
  - /MurderEvents/Details/{id} : event details
  - /Search?q=term : search results
  - /sitemap.xml : generated sitemap

Improvement ideas (implemented in this change)
- Header search and logo
- Infobox partial for details pages
- Sitemap generation and robots.txt
- Search UI and controller
- Accessibility and minor SEO improvements

Run locally
- dotnet build
- dotnet run
- Open the app at the address shown by Kestrel

Notes
- Update wwwroot/robots.txt to point to your production sitemap URL before deploying.
- Consider adding citations (DB schema) and image assets for entries in a future update.

