using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Security.Cryptography;
using System.Text;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Services
{
    public class PdfDocumentService : IPdfDocumentService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        private static readonly TimeSpan PdfCacheSlidingExpiration = TimeSpan.FromHours(12);
        private static readonly TimeSpan PdfCacheAbsoluteExpiration = TimeSpan.FromDays(7);

        public PdfDocumentService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<byte[]> GenerateProjectBriefPdfAsync(CancellationToken cancellationToken = default)
        {
            var events = await GetApprovedEventsAsync(cancellationToken);
            var totalEvents = events.Count;
            var coveredLocations = events.Select(e => e.LocationName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var earliestYear = events.Where(e => e.Year > 0).Select(e => e.Year).DefaultIfEmpty().Min();
            var latestYear = events.Where(e => e.Year > 0).Select(e => e.Year).DefaultIfEmpty().Max();
            var protectedCount = events.Count(e => e.IsProtected);
            var fingerprint = ComputeDatasetFingerprint(events);

            return GetOrCreateCachedDocument(PdfDocumentType.ProjectBrief, fingerprint, () => Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Content().PaddingTop(120).Column(col =>
                    {
                        col.Spacing(16);
                        col.Item().Text("The Murder Stone Archive").FontSize(34).SemiBold().FontColor(BrandPrimary);
                        col.Item().Text("Funding & Grant Project Brief").FontSize(20).FontColor(Colors.Grey.Darken2);
                        col.Item().Text("Concise summary for potential funders, grant panels, and institutional partners").FontSize(12).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(12).Element(Card).Column(card =>
                        {
                            card.Spacing(5);
                            card.Item().Text("Mission").SemiBold().FontSize(12);
                            card.Item().Text("Preserve and publish reliable educational records of historic murder stones and their social-historical context.");
                            card.Item().Text("This brief is intentionally concise; in-depth analysis is provided in the separate Research Pack.");
                        });
                    });
                    page.Footer().Element(ComposeStandardFooter);
                });

                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Header().Element(x => ComposeStandardHeader(x, "Project Brief"));
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        col.Spacing(12);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Element(MetricCard).Column(card =>
                            {
                                card.Item().Text("Approved records").FontSize(9).FontColor(Colors.Grey.Darken2);
                                card.Item().Text(totalEvents.ToString()).FontSize(20).SemiBold().FontColor(BrandPrimary);
                            });
                            row.RelativeItem().Element(MetricCard).Column(card =>
                            {
                                card.Item().Text("Locations covered").FontSize(9).FontColor(Colors.Grey.Darken2);
                                card.Item().Text(coveredLocations.ToString()).FontSize(20).SemiBold().FontColor(BrandPrimary);
                            });
                            row.RelativeItem().Element(MetricCard).Column(card =>
                            {
                                card.Item().Text("Protected records").FontSize(9).FontColor(Colors.Grey.Darken2);
                                card.Item().Text(protectedCount.ToString()).FontSize(20).SemiBold().FontColor(BrandPrimary);
                            });
                            row.RelativeItem().Element(MetricCard).Column(card =>
                            {
                                card.Item().Text("Chronological range").FontSize(9).FontColor(Colors.Grey.Darken2);
                                card.Item().Text($"{FormatYear(earliestYear)}–{FormatYear(latestYear)}").FontSize(16).SemiBold().FontColor(BrandPrimary);
                            });
                        });

                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(5);
                            card.Item().Text("Why this project merits funding").SemiBold().FontSize(12);
                            card.Item().Text("• Preserves vulnerable local-history knowledge in a structured public archive.");
                            card.Item().Text("• Provides free educational access for independent researchers and communities.");
                            card.Item().Text("• Combines moderation and verification workflows with transparent publication.");
                        });

                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(5);
                            card.Item().Text("Use of funds").SemiBold().FontSize(12);
                            card.Item().Text("• Hosting, uptime resilience, and secure operational infrastructure");
                            card.Item().Text("• Editorial moderation and quality assurance effort");
                            card.Item().Text("• Data curation and expansion into underserved geographic areas");
                            card.Item().Text("• Production of public educational resources (including research outputs)");
                        });

                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(5);
                            card.Item().Text("Deliverables and accountability").SemiBold().FontSize(12);
                            card.Item().Text("• Regularly updated public archive entries and quality-improved records");
                            card.Item().Text("• Public-facing educational resources and structured research materials");
                            card.Item().Text("• Impact reporting via usage and engagement analytics");
                        });

                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(5);
                            card.Item().Text("Contact and next step").SemiBold().FontSize(12);
                            card.Item().Text("For grant due diligence, partnerships, or institutional support discussions:");
                            card.Item().Text("contact@themurderstonearchive.com").SemiBold();
                        });
                    });
                    page.Footer().Element(ComposeStandardFooter);
                });
            }).GeneratePdf());
        }

        public async Task<byte[]> GenerateResearchPackOverviewPdfAsync(CancellationToken cancellationToken = default)
        {
            var events = await GetApprovedEventsAsync(cancellationToken);
            var analysisClusters = BuildLocationClusters(events).Take(12).ToList();
            var topCount = analysisClusters.Select(g => g.Events.Count).DefaultIfEmpty(0).Max();
            var mapGrid = BuildGeoGrid(events);
            var fingerprint = ComputeDatasetFingerprint(events);

            return GetOrCreateCachedDocument(PdfDocumentType.ResearchPackOverview, fingerprint, () => Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Header().Element(x => ComposeStandardHeader(x, "Research Pack • Overview"));
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        col.Spacing(12);

                        col.Item().Text("This overview is generated from approved records and identifies non-obvious spatial patterns using proximity hotspots, county concentrations, and potential linear alignments.");

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Element(MetricCard).Column(card =>
                            {
                                card.Item().Text("Approved records").FontSize(9).FontColor(Colors.Grey.Darken2);
                                card.Item().Text(events.Count.ToString()).FontSize(18).SemiBold().FontColor(BrandPrimary);
                            });
                            row.RelativeItem().Element(MetricCard).Column(card =>
                            {
                                card.Item().Text("Raw locations").FontSize(9).FontColor(Colors.Grey.Darken2);
                                card.Item().Text(events.Select(e => e.LocationName).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString()).FontSize(18).SemiBold().FontColor(BrandPrimary);
                            });
                            row.RelativeItem().Element(MetricCard).Column(card =>
                            {
                                card.Item().Text("Pattern clusters").FontSize(9).FontColor(Colors.Grey.Darken2);
                                card.Item().Text(analysisClusters.Count.ToString()).FontSize(18).SemiBold().FontColor(BrandPrimary);
                            });
                            row.RelativeItem().Element(MetricCard).Column(card =>
                            {
                                card.Item().Text("Protected").FontSize(9).FontColor(Colors.Grey.Darken2);
                                card.Item().Text(events.Count(e => e.IsProtected).ToString()).FontSize(18).SemiBold().FontColor(BrandPrimary);
                            });
                        });

                        col.Item().Text("Clustered pattern signals").SemiBold().FontSize(12);
                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(6);
                            foreach (var cluster in analysisClusters)
                            {
                                card.Item().Element(c => BarRow(c, cluster.DisplayName, cluster.Events.Count, topCount));
                            }
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2.1f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(2.5f);
                                columns.RelativeColumn(0.9f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(TableHeaderCell).Text("Cluster");
                                header.Cell().Element(TableHeaderCell).Text("Type");
                                header.Cell().Element(TableHeaderCell).Text("County");
                                header.Cell().Element(TableHeaderCell).Text("Evidence");
                                header.Cell().Element(TableHeaderCell).AlignRight().Text("Records");
                            });

                            foreach (var cluster in analysisClusters)
                            {
                                table.Cell().Element(TableBodyCell).Text(cluster.DisplayName);
                                table.Cell().Element(TableBodyCell).Text(cluster.ClusterType);
                                table.Cell().Element(TableBodyCell).Text(cluster.InferredCounty);
                                table.Cell().Element(TableBodyCell).Text(cluster.PatternEvidence);
                                table.Cell().Element(TableBodyCell).AlignRight().Text(cluster.Events.Count.ToString());
                            }
                        });

                        col.Item().Text("Map view: geospatial density grid").SemiBold().FontSize(12);
                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(5);
                            card.Item().Text("Approximate event density by coordinate grid (higher values indicate hotspots).").FontSize(9).FontColor(Colors.Grey.Darken2);

                            if (mapGrid.TotalPoints == 0)
                            {
                                card.Item().Text("Insufficient coordinate data to render map grid.");
                            }
                            else
                            {
                                card.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(1.6f);
                                        foreach (var _ in mapGrid.ColumnLabels)
                                        {
                                            columns.RelativeColumn(1);
                                        }
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(TableHeaderCell).Text("Latitude ↓ / Longitude →");
                                        foreach (var label in mapGrid.ColumnLabels)
                                        {
                                            header.Cell().Element(TableHeaderCell).AlignCenter().Text(label);
                                        }
                                    });

                                    foreach (var row in mapGrid.Rows)
                                    {
                                        table.Cell().Element(TableBodyCell).Text(row.Label);

                                        foreach (var count in row.Cells)
                                        {
                                            table.Cell().Element(c => HeatMapCell(c, count, mapGrid.MaxCount)).AlignCenter().Text(count.ToString());
                                        }
                                    }
                                });
                            }
                        });

                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(4);
                            card.Item().Text("Research guidance").SemiBold().FontSize(12);
                            card.Item().Text("• Cross-check date claims against primary sources.");
                            card.Item().Text("• Compare local newspaper reporting and archival records.");
                            card.Item().Text("• Record uncertainty explicitly in research notes.");
                            card.Item().Text("• Keep citation references tied to each factual claim.");
                        });
                    });
                    page.Footer().Element(ComposeStandardFooter);
                });
            }).GeneratePdf());
        }

        public async Task<byte[]> GenerateResearchPackTimelinePdfAsync(CancellationToken cancellationToken = default)
        {
            var events = await GetApprovedEventsAsync(cancellationToken);
            const int recordLimit = 300;
            var ordered = events
                .OrderBy(e => e.Year == 0 ? int.MaxValue : e.Year)
                .ThenBy(e => e.Title)
                .Take(recordLimit)
                .ToList();
            var fingerprint = ComputeDatasetFingerprint(events);

            return GetOrCreateCachedDocument(PdfDocumentType.ResearchPackTimeline, fingerprint, () => Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Header().Element(x => ComposeStandardHeader(x, "Research Pack • Chronological Timeline"));
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        col.Spacing(10);
                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(3);
                            card.Item().Text("Chronology index").SemiBold().FontSize(12);
                            card.Item().Text($"Showing {ordered.Count} approved records (cap: {recordLimit}) ordered by year then title.");
                            card.Item().Text("Rows with unknown years are listed after dated records.");
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);
                                columns.RelativeColumn(3.8f);
                                columns.RelativeColumn(2.4f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(TableHeaderCell).Text("Year");
                                header.Cell().Element(TableHeaderCell).Text("Title");
                                header.Cell().Element(TableHeaderCell).Text("Location");
                            });

                            foreach (var item in ordered)
                            {
                                table.Cell().Element(TableBodyCell).Text(FormatYear(item.Year));
                                table.Cell().Element(TableBodyCell).Text(item.Title);
                                table.Cell().Element(TableBodyCell).Text(item.LocationName);
                            }
                        });
                    });
                    page.Footer().Element(ComposeStandardFooter);
                });
            }).GeneratePdf());
        }

        public async Task<byte[]> GenerateResearchPackNotesPdfAsync(CancellationToken cancellationToken = default)
        {
            var events = await GetApprovedEventsAsync(cancellationToken);
            var trends = ExtractTrendAnalysis(events);
            var analytics = BuildDeepAnalysis(events, trends);
            var maxThemeCount = trends.Themes.Select(t => t.MatchCount).DefaultIfEmpty(0).Max();
            var analysisClusters = BuildLocationClusters(events).Take(10).ToList();
            var mapGrid = BuildGeoGrid(events);
            var fingerprint = ComputeDatasetFingerprint(events);

            return GetOrCreateCachedDocument(PdfDocumentType.ResearchPackNotes, fingerprint, () => Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Header().Element(x => ComposeStandardHeader(x, "Research Pack • Analytical Notes"));
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        col.Spacing(12);

                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(4);
                            card.Item().Text("Interpretation summary").SemiBold().FontSize(12);
                            card.Item().Text("Use this section to prioritize research effort by chronology, location concentration, protection status, recurring narrative themes, and statistical anomaly signals.");
                        });

                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(4);
                            card.Item().Text("Hotspot significance (nearest-neighbour)").SemiBold().FontSize(12);
                            card.Item().Text($"Coordinate points: {analytics.NearestNeighbour.PointCount}");
                            card.Item().Text($"Observed mean nearest distance: {analytics.NearestNeighbour.ObservedMeanDistanceKm:0.00} km");
                            card.Item().Text($"Expected random mean distance: {analytics.NearestNeighbour.ExpectedMeanDistanceKm:0.00} km");
                            card.Item().Text($"R ratio: {analytics.NearestNeighbour.RatioR:0.00} | Z-score: {analytics.NearestNeighbour.ZScore:0.00}");
                        });

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Element(MetricCard).Column(card =>
                            {
                                card.Item().Text("Protected records").FontSize(9).FontColor(Colors.Grey.Darken2);
                                card.Item().Text(events.Count(e => e.IsProtected).ToString()).FontSize(17).SemiBold().FontColor(BrandPrimary);
                            });
                            row.RelativeItem().Element(MetricCard).Column(card =>
                            {
                                card.Item().Text("Not protected").FontSize(9).FontColor(Colors.Grey.Darken2);
                                card.Item().Text(events.Count(e => !e.IsProtected).ToString()).FontSize(17).SemiBold().FontColor(BrandPrimary);
                            });
                        });

                        col.Item().Text("Description trend themes").SemiBold().FontSize(12);
                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(6);
                            foreach (var theme in trends.Themes.Take(6))
                            {
                                card.Item().Element(c => BarRow(c, theme.Name, theme.MatchCount, maxThemeCount));
                            }
                        });

                        col.Item().Text("Most recurring descriptive terms").SemiBold().FontSize(12);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(TableHeaderCell).Text("Term");
                                header.Cell().Element(TableHeaderCell).AlignRight().Text("Mentions");
                            });

                            foreach (var term in trends.TopTerms)
                            {
                                table.Cell().Element(TableBodyCell).Text(term.Term);
                                table.Cell().Element(TableBodyCell).AlignRight().Text(term.Count.ToString());
                            }
                        });

                        col.Item().Text("Temporal anomalies and county shifts").SemiBold().FontSize(12);
                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(6);

                            card.Item().Text("Decade anomaly signals").SemiBold().FontSize(10.5f);
                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.4f);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderCell).Text("Decade");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Records");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Z");
                                });

                                foreach (var item in analytics.TemporalAnomalies)
                                {
                                    table.Cell().Element(TableBodyCell).Text(item.DecadeStartYear.ToString());
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.EventCount.ToString());
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.ZScore.ToString("0.00"));
                                }
                            });

                            card.Item().Text("County chronology shift").SemiBold().FontSize(10.5f);
                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(1);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderCell).Text("County");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Early");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Late");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Shift yrs");
                                });

                                foreach (var item in analytics.SpatioTemporalCountyShifts.Take(8))
                                {
                                    table.Cell().Element(TableBodyCell).Text(item.County);
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.EarlyPeriodRecords.ToString());
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.LatePeriodRecords.ToString());
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.MeanYearShift.ToString("0.0"));
                                }
                            });
                        });

                        col.Item().Text("Map insights").SemiBold().FontSize(12);
                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(5);
                            card.Item().Text("Top analytical clusters with county context, centroid coordinates, and inferred pattern evidence.").FontSize(9).FontColor(Colors.Grey.Darken2);
                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.8f);
                                    columns.RelativeColumn(1f);
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(1.8f);
                                    columns.RelativeColumn(1.1f);
                                    columns.RelativeColumn(0.8f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderCell).Text("Cluster");
                                    header.Cell().Element(TableHeaderCell).Text("Type");
                                    header.Cell().Element(TableHeaderCell).Text("County");
                                    header.Cell().Element(TableHeaderCell).Text("Evidence");
                                    header.Cell().Element(TableHeaderCell).Text("Centroid");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Records");
                                });

                                foreach (var cluster in analysisClusters)
                                {
                                    var centroid = cluster.CentroidLatitude.HasValue && cluster.CentroidLongitude.HasValue
                                        ? $"{cluster.CentroidLatitude.Value:0.000}, {cluster.CentroidLongitude.Value:0.000}"
                                        : "n/a";

                                    table.Cell().Element(TableBodyCell).Text(cluster.DisplayName);
                                    table.Cell().Element(TableBodyCell).Text(cluster.ClusterType);
                                    table.Cell().Element(TableBodyCell).Text(cluster.InferredCounty);
                                    table.Cell().Element(TableBodyCell).Text(cluster.PatternEvidence);
                                    table.Cell().Element(TableBodyCell).Text(centroid);
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(cluster.Events.Count.ToString());
                                }
                            });

                            if (mapGrid.TotalPoints > 0)
                            {
                                card.Item().Text($"Map grid included {mapGrid.TotalPoints} events with usable coordinates.").FontSize(9).FontColor(Colors.Grey.Darken2);
                            }
                        });

                        col.Item().Text("Narrative evolution and thematic links").SemiBold().FontSize(12);
                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(6);
                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(0.8f);
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(2.2f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderCell).Text("Era");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Records");
                                    header.Cell().Element(TableHeaderCell).Text("Top theme");
                                    header.Cell().Element(TableHeaderCell).Text("Top terms");
                                });

                                foreach (var item in analytics.NarrativeEvolutionByEra)
                                {
                                    table.Cell().Element(TableBodyCell).Text(item.Era);
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.Records.ToString());
                                    table.Cell().Element(TableBodyCell).Text(item.TopTheme);
                                    table.Cell().Element(TableBodyCell).Text(item.TopTermsSummary);
                                }
                            });

                            card.Item().Text("Keyword co-occurrence network (top pairs)").SemiBold().FontSize(10.5f);
                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(0.8f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderCell).Text("Term A");
                                    header.Cell().Element(TableHeaderCell).Text("Term B");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Co-mentions");
                                });

                                foreach (var item in analytics.KeywordCoOccurrences.Take(10))
                                {
                                    table.Cell().Element(TableBodyCell).Text(item.TermA);
                                    table.Cell().Element(TableBodyCell).Text(item.TermB);
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.CoOccurrenceCount.ToString());
                                }
                            });
                        });

                        col.Item().Text("County profiles and alignment confidence").SemiBold().FontSize(12);
                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(6);
                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.3f);
                                    columns.RelativeColumn(0.7f);
                                    columns.RelativeColumn(0.9f);
                                    columns.RelativeColumn(0.9f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderCell).Text("County");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Records");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Protected %");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Decades");
                                });

                                foreach (var item in analytics.CountyProfiles.Take(8))
                                {
                                    table.Cell().Element(TableBodyCell).Text(item.County);
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.Records.ToString());
                                    table.Cell().Element(TableBodyCell).AlignRight().Text((item.ProtectedRate * 100).ToString("0"));
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.DecadeSpread.ToString());
                                }
                            });

                            card.Item().Text("Linear-alignment confidence").SemiBold().FontSize(10.5f);
                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.6f);
                                    columns.RelativeColumn(1f);
                                    columns.RelativeColumn(0.8f);
                                    columns.RelativeColumn(0.8f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderCell).Text("Cluster");
                                    header.Cell().Element(TableHeaderCell).Text("County");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Span km");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Score");
                                });

                                foreach (var item in analytics.AlignmentConfidence.Take(8))
                                {
                                    table.Cell().Element(TableBodyCell).Text(item.ClusterName);
                                    table.Cell().Element(TableBodyCell).Text(item.County);
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.SpanKm.ToString("0.0"));
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.ConfidenceScore.ToString("0"));
                                }
                            });
                        });

                        col.Item().Text("Risk, uncertainty, outliers, and data quality").SemiBold().FontSize(12);
                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(6);

                            card.Item().Text("Priority protection-risk list").SemiBold().FontSize(10.5f);
                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2.2f);
                                    columns.RelativeColumn(1.1f);
                                    columns.RelativeColumn(0.7f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderCell).Text("Event");
                                    header.Cell().Element(TableHeaderCell).Text("County");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Risk");
                                });

                                foreach (var item in analytics.ProtectionRiskScores.Take(8))
                                {
                                    table.Cell().Element(TableBodyCell).Text(item.Title);
                                    table.Cell().Element(TableBodyCell).Text(item.County);
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.Score.ToString("0"));
                                }
                            });

                            card.Item().Text("Highest uncertainty records").SemiBold().FontSize(10.5f);
                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.8f);
                                    columns.RelativeColumn(2.2f);
                                    columns.RelativeColumn(0.7f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderCell).Text("Event");
                                    header.Cell().Element(TableHeaderCell).Text("Uncertainty drivers");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Score");
                                });

                                foreach (var item in analytics.UncertaintyScores.Take(8))
                                {
                                    table.Cell().Element(TableBodyCell).Text(item.Title);
                                    table.Cell().Element(TableBodyCell).Text(item.ReasonSummary);
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.UncertaintyScore.ToString());
                                }
                            });

                            card.Item().Text("Geospatial outliers").SemiBold().FontSize(10.5f);
                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.7f);
                                    columns.RelativeColumn(1f);
                                    columns.RelativeColumn(0.8f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderCell).Text("Event");
                                    header.Cell().Element(TableHeaderCell).Text("County");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Nearest km");
                                });

                                foreach (var item in analytics.Outliers.Take(8))
                                {
                                    table.Cell().Element(TableBodyCell).Text(item.Title);
                                    table.Cell().Element(TableBodyCell).Text(item.County);
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.NearestDistanceKm.ToString("0.0"));
                                }
                            });

                            card.Item().Text("Source-quality stratification").SemiBold().FontSize(10.5f);
                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.4f);
                                    columns.RelativeColumn(0.8f);
                                    columns.RelativeColumn(2.3f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderCell).Text("Band");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Records");
                                    header.Cell().Element(TableHeaderCell).Text("Notes");
                                });

                                foreach (var item in analytics.SourceQualityBands)
                                {
                                    table.Cell().Element(TableBodyCell).Text(item.Band);
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.RecordCount.ToString());
                                    table.Cell().Element(TableBodyCell).Text(item.Notes);
                                }
                            });

                            card.Item().Text("Completeness over time").SemiBold().FontSize(10.5f);
                            card.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(0.8f);
                                    columns.RelativeColumn(1f);
                                    columns.RelativeColumn(1f);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeaderCell).Text("Era");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Records");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Coords %");
                                    header.Cell().Element(TableHeaderCell).AlignRight().Text("Rich text %");
                                });

                                foreach (var item in analytics.CompletenessByEra.Take(10))
                                {
                                    table.Cell().Element(TableBodyCell).Text(item.EraLabel);
                                    table.Cell().Element(TableBodyCell).AlignRight().Text(item.Records.ToString());
                                    table.Cell().Element(TableBodyCell).AlignRight().Text((item.CoordinateKnownRate * 100).ToString("0"));
                                    table.Cell().Element(TableBodyCell).AlignRight().Text((item.NarrativeRichRate * 100).ToString("0"));
                                }
                            });
                        });

                        col.Item().Element(Card).Column(card =>
                        {
                            card.Spacing(5);
                            card.Item().Text("Suggested researcher workflow").SemiBold().FontSize(12);
                            card.Item().Text("1) Start from timeline entries with precise years.");
                            card.Item().Text("2) Prioritize locations with highest record density and statistically significant clustering.");
                            card.Item().Text("3) Prioritize high-risk and high-uncertainty records for deeper verification.");
                            card.Item().Text("4) Compare event narratives against at least two independent historical sources.");
                            card.Item().Text("5) Flag conflicts and unknowns explicitly in your notes.");
                        });

                        col.Item().Text("Sample records for verification").SemiBold().FontSize(12);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(55);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(TableHeaderCell).Text("Year");
                                header.Cell().Element(TableHeaderCell).Text("Title");
                                header.Cell().Element(TableHeaderCell).Text("Location");
                            });

                            foreach (var item in events
                                         .Where(e => e.Year > 0)
                                         .OrderBy(e => e.Year)
                                         .ThenBy(e => e.Title)
                                         .Take(30))
                            {
                                table.Cell().Element(TableBodyCell).Text(item.Year.ToString());
                                table.Cell().Element(TableBodyCell).Text(item.Title);
                                table.Cell().Element(TableBodyCell).Text(item.LocationName);
                            }
                        });
                    });
                    page.Footer().Element(ComposeStandardFooter);
                });
            }).GeneratePdf());
        }

        private async Task<List<EventSnapshot>> GetApprovedEventsAsync(CancellationToken cancellationToken)
        {
            var events = await _context.MurderEvents
                .AsNoTracking()
                .WithLocation()
                .ApprovedAndNotLost()
                .Select(e => new EventSnapshot
                {
                    Id = e.Id,
                    Title = e.Title,
                    Description = e.Description,
                    Year = e.Year,
                    LocationName = e.Location != null ? e.Location.Name : "Unknown",
                    Latitude = e.Location != null ? e.Location.Latitude : null,
                    Longitude = e.Location != null ? e.Location.Longitude : null,
                    IsProtected = e.IsProtected,
                    IsLost = e.IsLost
                })
                .ToListAsync(cancellationToken);

            foreach (var item in events)
            {
                item.NormalizedLocationName = NormalizeLocationName(item.LocationName);
                item.InferredCounty = InferCounty(item.LocationName, item.Latitude, item.Longitude);
            }

            return events;
        }

        private static string ComputeDatasetFingerprint(IEnumerable<EventSnapshot> events)
        {
            var ordered = events
                .OrderBy(e => e.Id)
                .ThenBy(e => e.Title, StringComparer.Ordinal)
                .ThenBy(e => e.Year)
                .ToList();

            var sb = new StringBuilder();
            foreach (var e in ordered)
            {
                sb.Append(e.Id).Append('|')
                  .Append(e.Title).Append('|')
                  .Append(e.Description).Append('|')
                  .Append(e.Year).Append('|')
                  .Append(e.LocationName).Append('|')
                  .Append(e.Latitude?.ToString("0.######") ?? string.Empty).Append('|')
                  .Append(e.Longitude?.ToString("0.######") ?? string.Empty).Append('|')
                  .Append(e.IsProtected).Append('|')
                  .Append(e.IsLost).AppendLine();
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private byte[] GetOrCreateCachedDocument(PdfDocumentType documentType, string fingerprint, Func<byte[]> factory)
        {
            var cacheKey = $"pdf:{documentType}";

            if (_cache.TryGetValue<CachedPdfDocument>(cacheKey, out var cached) &&
                cached is not null &&
                string.Equals(cached.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return cached.Bytes;
            }

            var bytes = factory();

            _cache.Set(cacheKey,
                new CachedPdfDocument
                {
                    Fingerprint = fingerprint,
                    Bytes = bytes
                },
                new MemoryCacheEntryOptions
                {
                    SlidingExpiration = PdfCacheSlidingExpiration,
                    AbsoluteExpirationRelativeToNow = PdfCacheAbsoluteExpiration
                });

            return bytes;
        }

        private static string NormalizeLocationName(string locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName))
            {
                return "unknown";
            }

            var normalized = locationName.Trim().ToLowerInvariant();
            normalized = normalized.Replace("near ", string.Empty);
            normalized = normalized.Replace("st.", "saint");
            normalized = normalized.Replace("  ", " ");
            normalized = new string(normalized.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
            return normalized.Trim();
        }

        private static IReadOnlyList<LocationCluster> BuildLocationClusters(IReadOnlyList<EventSnapshot> events)
        {
            const double maxClusterDistanceKm = 25;
            const int minEventsPerCluster = 2;
            const int minEventsForAlignment = 3;
            const double maxAlignmentOffsetKm = 1.8;
            const double minAlignmentSpanKm = 10;

            var clusters = new List<LocationCluster>();
            clusters.AddRange(BuildProximityClusters(events, maxClusterDistanceKm, minEventsPerCluster));
            clusters.AddRange(BuildCountyClusters(events, minEventsPerCluster));
            clusters.AddRange(BuildAlignmentClusters(events, minEventsForAlignment, maxAlignmentOffsetKm, minAlignmentSpanKm));

            foreach (var cluster in clusters)
            {
                var coords = cluster.Events.Where(e => e.Latitude.HasValue && e.Longitude.HasValue).ToList();
                if (coords.Count > 0)
                {
                    cluster.CentroidLatitude = coords.Average(e => e.Latitude!.Value);
                    cluster.CentroidLongitude = coords.Average(e => e.Longitude!.Value);
                }

                cluster.InferredCounty = DetermineDominantCounty(cluster.Events);
            }

            return clusters
                .Where(c => c.Events.Count >= minEventsPerCluster)
                .GroupBy(c => $"{c.ClusterType}|{string.Join(',', c.Events.Select(e => e.Id).OrderBy(id => id))}")
                .Select(g => g.First())
                .OrderByDescending(c => c.Events.Count)
                .ThenBy(c => c.ClusterType)
                .ThenBy(c => c.DisplayName)
                .ToList();
        }

        private static IEnumerable<LocationCluster> BuildProximityClusters(
            IReadOnlyList<EventSnapshot> events,
            double maxClusterDistanceKm,
            int minEvents)
        {
            var withCoords = events
                .Where(e => e.Latitude.HasValue && e.Longitude.HasValue)
                .ToList();

            var unassigned = new HashSet<int>(Enumerable.Range(0, withCoords.Count));
            var clusters = new List<LocationCluster>();

            while (unassigned.Count > 0)
            {
                var seedIndex = unassigned.First();
                unassigned.Remove(seedIndex);

                var members = new List<int> { seedIndex };
                var queue = new Queue<int>();
                queue.Enqueue(seedIndex);

                while (queue.Count > 0)
                {
                    var currentIndex = queue.Dequeue();
                    var current = withCoords[currentIndex];

                    var neighbours = unassigned
                        .Where(index => HaversineDistanceKm(
                            current.Latitude!.Value,
                            current.Longitude!.Value,
                            withCoords[index].Latitude!.Value,
                            withCoords[index].Longitude!.Value) <= maxClusterDistanceKm)
                        .ToList();

                    foreach (var neighbour in neighbours)
                    {
                        unassigned.Remove(neighbour);
                        members.Add(neighbour);
                        queue.Enqueue(neighbour);
                    }
                }

                if (members.Count < minEvents)
                {
                    continue;
                }

                var clusterEvents = members.Select(index => withCoords[index]).ToList();
                var centroidLat = clusterEvents.Average(e => e.Latitude!.Value);
                var centroidLon = clusterEvents.Average(e => e.Longitude!.Value);
                var radiusKm = clusterEvents.Max(e => HaversineDistanceKm(centroidLat, centroidLon, e.Latitude!.Value, e.Longitude!.Value));
                var county = DetermineDominantCounty(clusterEvents);

                var cluster = new LocationCluster
                {
                    NormalizedName = $"proximity-{centroidLat:0.000}-{centroidLon:0.000}",
                    DisplayName = county != "Unknown"
                        ? $"{county} proximity hotspot"
                        : "Proximity hotspot",
                    ClusterType = "Proximity",
                    PatternEvidence = $"{clusterEvents.Count} records within ~{radiusKm:0.0} km of centroid",
                    InferredCounty = county,
                    CentroidLatitude = centroidLat,
                    CentroidLongitude = centroidLon
                };

                cluster.Events.AddRange(clusterEvents);
                clusters.Add(cluster);
            }

            return clusters;
        }

        private static IEnumerable<LocationCluster> BuildCountyClusters(IReadOnlyList<EventSnapshot> events, int minEvents)
        {
            return events
                .Where(e => !string.IsNullOrWhiteSpace(e.InferredCounty) && !string.Equals(e.InferredCounty, "Unknown", StringComparison.OrdinalIgnoreCase))
                .GroupBy(e => e.InferredCounty, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() >= minEvents)
                .Select(g =>
                {
                    var county = g.First().InferredCounty;
                    var cluster = new LocationCluster
                    {
                        NormalizedName = $"county-{NormalizeLocationName(county)}",
                        DisplayName = $"{county} county concentration",
                        ClusterType = "County",
                        PatternEvidence = $"{g.Count()} records inferred in the same county",
                        InferredCounty = county
                    };

                    cluster.Events.AddRange(g);
                    return cluster;
                })
                .ToList();
        }

        private static IEnumerable<LocationCluster> BuildAlignmentClusters(
            IReadOnlyList<EventSnapshot> events,
            int minEvents,
            double maxOffsetKm,
            double minSpanKm)
        {
            var output = new List<LocationCluster>();

            var countyGroups = events
                .Where(e => e.Latitude.HasValue && e.Longitude.HasValue)
                .GroupBy(e => e.InferredCounty, StringComparer.OrdinalIgnoreCase);

            foreach (var countyGroup in countyGroups)
            {
                var points = countyGroup.ToList();
                if (points.Count < minEvents)
                {
                    continue;
                }

                var pair = FindFarthestPair(points);
                if (pair.First == null || pair.Second == null || pair.DistanceKm < minSpanKm)
                {
                    continue;
                }

                var aligned = points
                    .Where(point => DistancePointToLineKm(point, pair.First, pair.Second) <= maxOffsetKm)
                    .ToList();

                if (aligned.Count < minEvents)
                {
                    continue;
                }

                var lineSpanKm = ComputeProjectedSpanKm(aligned, pair.First, pair.Second);
                if (lineSpanKm < minSpanKm)
                {
                    continue;
                }

                var county = DetermineDominantCounty(aligned);
                var cluster = new LocationCluster
                {
                    NormalizedName = $"alignment-{NormalizeLocationName(county)}-{pair.First.Id}-{pair.Second.Id}",
                    DisplayName = county != "Unknown"
                        ? $"{county} linear alignment"
                        : "Linear alignment pattern",
                    ClusterType = "Alignment",
                    PatternEvidence = $"{aligned.Count} records aligned within ~{maxOffsetKm:0.0} km across ~{lineSpanKm:0.0} km",
                    InferredCounty = county
                };

                cluster.Events.AddRange(aligned);
                output.Add(cluster);
            }

            return output;
        }

        private static (EventSnapshot? First, EventSnapshot? Second, double DistanceKm) FindFarthestPair(IReadOnlyList<EventSnapshot> points)
        {
            EventSnapshot? first = null;
            EventSnapshot? second = null;
            var maxDistance = 0d;

            for (var i = 0; i < points.Count; i++)
            {
                for (var j = i + 1; j < points.Count; j++)
                {
                    var distance = HaversineDistanceKm(
                        points[i].Latitude!.Value,
                        points[i].Longitude!.Value,
                        points[j].Latitude!.Value,
                        points[j].Longitude!.Value);

                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        first = points[i];
                        second = points[j];
                    }
                }
            }

            return (first, second, maxDistance);
        }

        private static double DistancePointToLineKm(EventSnapshot point, EventSnapshot lineA, EventSnapshot lineB)
        {
            var referenceLatitude = (lineA.Latitude!.Value + lineB.Latitude!.Value) / 2d;
            var originLatitude = lineA.Latitude.Value;
            var originLongitude = lineA.Longitude!.Value;

            var ax = 0d;
            var ay = 0d;
            var bx = LongitudeDeltaToKm(lineB.Longitude!.Value - originLongitude, referenceLatitude);
            var by = LatitudeDeltaToKm(lineB.Latitude!.Value - originLatitude);
            var px = LongitudeDeltaToKm(point.Longitude!.Value - originLongitude, referenceLatitude);
            var py = LatitudeDeltaToKm(point.Latitude!.Value - originLatitude);

            var lineLength = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            if (lineLength < 0.001)
            {
                return Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
            }

            var cross = Math.Abs((bx - ax) * (py - ay) - (by - ay) * (px - ax));
            return cross / lineLength;
        }

        private static double ComputeProjectedSpanKm(IReadOnlyList<EventSnapshot> points, EventSnapshot lineA, EventSnapshot lineB)
        {
            var referenceLatitude = (lineA.Latitude!.Value + lineB.Latitude!.Value) / 2d;
            var originLatitude = lineA.Latitude.Value;
            var originLongitude = lineA.Longitude!.Value;

            var axisX = LongitudeDeltaToKm(lineB.Longitude!.Value - originLongitude, referenceLatitude);
            var axisY = LatitudeDeltaToKm(lineB.Latitude!.Value - originLatitude);
            var axisLength = Math.Sqrt(axisX * axisX + axisY * axisY);

            if (axisLength < 0.001)
            {
                return 0;
            }

            var unitX = axisX / axisLength;
            var unitY = axisY / axisLength;

            var projections = points
                .Select(point =>
                {
                    var pointX = LongitudeDeltaToKm(point.Longitude!.Value - originLongitude, referenceLatitude);
                    var pointY = LatitudeDeltaToKm(point.Latitude!.Value - originLatitude);
                    return pointX * unitX + pointY * unitY;
                })
                .ToList();

            return projections.Max() - projections.Min();
        }

        private static string DetermineDominantCounty(IEnumerable<EventSnapshot> events)
        {
            return events
                .Select(e => e.InferredCounty)
                .Where(c => !string.IsNullOrWhiteSpace(c) && !string.Equals(c, "Unknown", StringComparison.OrdinalIgnoreCase))
                .GroupBy(c => c!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => g.Key)
                .FirstOrDefault() ?? "Unknown";
        }

        private static double LatitudeDeltaToKm(double latitudeDelta) => latitudeDelta * 111.32;

        private static double LongitudeDeltaToKm(double longitudeDelta, double referenceLatitude)
            => longitudeDelta * 111.32 * Math.Cos(DegreesToRadians(referenceLatitude));

        private static string InferCounty(string locationName, double? latitude, double? longitude)
        {
            if (!string.IsNullOrWhiteSpace(locationName))
            {
                var lower = locationName.ToLowerInvariant();
                foreach (var county in CountyKeywords.Keys)
                {
                    if (lower.Contains(county, StringComparison.OrdinalIgnoreCase))
                    {
                        return CountyKeywords[county];
                    }
                }
            }

            if (latitude.HasValue && longitude.HasValue)
            {
                var nearest = CountyCentroids
                    .Select(c => new
                    {
                        c.Key,
                        Distance = HaversineDistanceKm(latitude.Value, longitude.Value, c.Value.Latitude, c.Value.Longitude)
                    })
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();

                if (nearest != null && nearest.Distance <= 45)
                {
                    return nearest.Key;
                }
            }

            return "Unknown";
        }

        private static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371;

            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c;
        }

        private static double EstimateBoundingAreaKm2(double minLat, double maxLat, double minLon, double maxLon)
        {
            var midLat = (minLat + maxLat) / 2d;
            var heightKm = LatitudeDeltaToKm(Math.Abs(maxLat - minLat));
            var widthKm = LongitudeDeltaToKm(Math.Abs(maxLon - minLon), midLat);
            return Math.Abs(heightKm * widthKm);
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

        private static TrendAnalysis ExtractTrendAnalysis(IReadOnlyList<EventSnapshot> events)
        {
            var themes = new List<TrendTheme>();
            foreach (var trend in TrendKeywords)
            {
                var count = events.Count(e => ContainsAnyKeyword(e.Description, trend.Value));
                themes.Add(new TrendTheme
                {
                    Name = trend.Key,
                    MatchCount = count
                });
            }

            var decadeBuckets = events
                .Where(e => e.Year > 0)
                .GroupBy(e => (e.Year / 10) * 10)
                .Select(g => new TrendDecade
                {
                    DecadeStartYear = g.Key,
                    EventCount = g.Count()
                })
                .OrderBy(g => g.DecadeStartYear)
                .ToList();

            var topTerms = events
                .SelectMany(e => TokenizeDescription(e.Description))
                .GroupBy(token => token)
                .Select(g => new TrendTerm
                {
                    Term = g.Key,
                    Count = g.Count()
                })
                .Where(x => x.Count >= 3)
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Term)
                .Take(12)
                .ToList();

            return new TrendAnalysis
            {
                Themes = themes.OrderByDescending(t => t.MatchCount).ThenBy(t => t.Name).ToList(),
                DecadeSeries = decadeBuckets,
                TopTerms = topTerms
            };
        }

        private static DeepAnalysisResult BuildDeepAnalysis(IReadOnlyList<EventSnapshot> events, TrendAnalysis trends)
        {
            var nearestNeighbour = CalculateNearestNeighbourStats(events);

            return new DeepAnalysisResult
            {
                TemporalAnomalies = BuildTemporalAnomalies(events),
                SpatioTemporalCountyShifts = BuildSpatioTemporalCountyShifts(events),
                NearestNeighbour = nearestNeighbour,
                NarrativeEvolutionByEra = BuildNarrativeEvolutionByEra(events),
                CountyProfiles = BuildCountyProfiles(events, trends),
                ProtectionRiskScores = BuildProtectionRiskScores(events),
                UncertaintyScores = BuildUncertaintyScores(events),
                Outliers = BuildOutliers(events, nearestNeighbour),
                KeywordCoOccurrences = BuildKeywordCoOccurrences(events),
                SourceQualityBands = BuildSourceQualityBands(events),
                AlignmentConfidence = BuildAlignmentConfidence(events),
                CompletenessByEra = BuildCompletenessByEra(events)
            };
        }

        private static List<TemporalAnomaly> BuildTemporalAnomalies(IReadOnlyList<EventSnapshot> events)
        {
            var decadeCounts = events
                .Where(e => e.Year > 0)
                .GroupBy(e => (e.Year / 10) * 10)
                .Select(g => new { Decade = g.Key, Count = g.Count() })
                .OrderBy(x => x.Decade)
                .ToList();

            if (decadeCounts.Count == 0)
            {
                return [];
            }

            var average = decadeCounts.Average(x => x.Count);
            var stdDev = Math.Sqrt(decadeCounts.Average(x => Math.Pow(x.Count - average, 2)));
            if (stdDev < 0.001)
            {
                return [];
            }

            return decadeCounts
                .Select(x => new TemporalAnomaly
                {
                    DecadeStartYear = x.Decade,
                    EventCount = x.Count,
                    ZScore = (x.Count - average) / stdDev
                })
                .Where(x => Math.Abs(x.ZScore) >= 1.2)
                .OrderByDescending(x => Math.Abs(x.ZScore))
                .Take(8)
                .ToList();
        }

        private static List<CountyShift> BuildSpatioTemporalCountyShifts(IReadOnlyList<EventSnapshot> events)
        {
            return events
                .Where(e => e.Year > 0)
                .Where(e => !string.IsNullOrWhiteSpace(e.InferredCounty) && !string.Equals(e.InferredCounty, "Unknown", StringComparison.OrdinalIgnoreCase))
                .GroupBy(e => e.InferredCounty, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var ordered = g.OrderBy(e => e.Year).ToList();
                    var early = ordered.Where(e => e.Year < 1750).ToList();
                    var late = ordered.Where(e => e.Year >= 1750).ToList();

                    var earlyMean = early.Count > 0 ? early.Average(e => e.Year) : 0;
                    var lateMean = late.Count > 0 ? late.Average(e => e.Year) : 0;

                    return new CountyShift
                    {
                        County = g.Key,
                        TotalRecords = g.Count(),
                        EarlyPeriodRecords = early.Count,
                        LatePeriodRecords = late.Count,
                        MeanYearShift = early.Count > 0 && late.Count > 0 ? lateMean - earlyMean : 0
                    };
                })
                .Where(x => x.TotalRecords >= 2)
                .OrderByDescending(x => Math.Abs(x.MeanYearShift))
                .ThenByDescending(x => x.TotalRecords)
                .Take(10)
                .ToList();
        }

        private static List<NarrativeEraProfile> BuildNarrativeEvolutionByEra(IReadOnlyList<EventSnapshot> events)
        {
            var eras = new[]
            {
                new { Name = "Pre-1600", Min = int.MinValue, Max = 1599 },
                new { Name = "1600-1799", Min = 1600, Max = 1799 },
                new { Name = "1800+", Min = 1800, Max = int.MaxValue }
            };

            var profiles = new List<NarrativeEraProfile>();
            foreach (var era in eras)
            {
                var eraEvents = events.Where(e => e.Year >= era.Min && e.Year <= era.Max).ToList();
                if (eraEvents.Count == 0)
                {
                    continue;
                }

                var topTerms = eraEvents
                    .SelectMany(e => TokenizeDescription(e.Description))
                    .GroupBy(t => t)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key)
                    .Take(4)
                    .Select(g => g.Key)
                    .ToList();

                var topTheme = TrendKeywords
                    .Select(kvp => new
                    {
                        Name = kvp.Key,
                        Count = eraEvents.Count(e => ContainsAnyKeyword(e.Description, kvp.Value))
                    })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Name)
                    .FirstOrDefault();

                profiles.Add(new NarrativeEraProfile
                {
                    Era = era.Name,
                    Records = eraEvents.Count,
                    TopTheme = topTheme?.Name ?? "n/a",
                    TopTermsSummary = string.Join(", ", topTerms)
                });
            }

            return profiles;
        }

        private static List<CountyProfile> BuildCountyProfiles(IReadOnlyList<EventSnapshot> events, TrendAnalysis trends)
        {
            return events
                .Where(e => !string.IsNullOrWhiteSpace(e.InferredCounty) && !string.Equals(e.InferredCounty, "Unknown", StringComparison.OrdinalIgnoreCase))
                .GroupBy(e => e.InferredCounty, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var decadeSpread = g.Where(x => x.Year > 0).Select(x => (x.Year / 10) * 10).Distinct().Count();
                    var protectedRate = g.Any() ? (double)g.Count(x => x.IsProtected) / g.Count() : 0;
                    var themeDensity = trends.Themes.Count == 0 ? 0 : trends.Themes.Average(t => g.Count(e => ContainsAnyKeyword(e.Description, TrendKeywords[t.Name])));

                    return new CountyProfile
                    {
                        County = g.Key,
                        Records = g.Count(),
                        ProtectedRate = protectedRate,
                        DecadeSpread = decadeSpread,
                        ThemeDensity = themeDensity
                    };
                })
                .Where(x => x.Records >= 2)
                .OrderByDescending(x => x.Records)
                .ThenBy(x => x.County)
                .Take(10)
                .ToList();
        }

        private static NearestNeighbourStats CalculateNearestNeighbourStats(IReadOnlyList<EventSnapshot> events)
        {
            var points = events.Where(e => e.Latitude.HasValue && e.Longitude.HasValue).ToList();
            if (points.Count < 3)
            {
                return new NearestNeighbourStats
                {
                    PointCount = points.Count,
                    ObservedMeanDistanceKm = 0,
                    ExpectedMeanDistanceKm = 0,
                    RatioR = 0,
                    ZScore = 0
                };
            }

            var nearestDistances = new List<double>(points.Count);
            foreach (var point in points)
            {
                var nearest = points
                    .Where(p => p.Id != point.Id)
                    .Select(p => HaversineDistanceKm(point.Latitude!.Value, point.Longitude!.Value, p.Latitude!.Value, p.Longitude!.Value))
                    .DefaultIfEmpty(double.MaxValue)
                    .Min();

                if (!double.IsInfinity(nearest) && nearest < double.MaxValue)
                {
                    nearestDistances.Add(nearest);
                }
            }

            if (nearestDistances.Count == 0)
            {
                return new NearestNeighbourStats
                {
                    PointCount = points.Count,
                    ObservedMeanDistanceKm = 0,
                    ExpectedMeanDistanceKm = 0,
                    RatioR = 0,
                    ZScore = 0
                };
            }

            var minLat = points.Min(p => p.Latitude!.Value);
            var maxLat = points.Max(p => p.Latitude!.Value);
            var minLon = points.Min(p => p.Longitude!.Value);
            var maxLon = points.Max(p => p.Longitude!.Value);
            var areaKm2 = EstimateBoundingAreaKm2(minLat, maxLat, minLon, maxLon);
            if (areaKm2 <= 0.001)
            {
                areaKm2 = 1;
            }

            var n = points.Count;
            var observedMean = nearestDistances.Average();
            var expectedMean = 0.5 * Math.Sqrt(areaKm2 / n);
            var ratio = expectedMean <= 0 ? 0 : observedMean / expectedMean;
            var standardError = 0.26136 * Math.Sqrt(areaKm2 / (n * n));
            var z = standardError <= 0 ? 0 : (observedMean - expectedMean) / standardError;

            return new NearestNeighbourStats
            {
                PointCount = n,
                ObservedMeanDistanceKm = observedMean,
                ExpectedMeanDistanceKm = expectedMean,
                RatioR = ratio,
                ZScore = z
            };
        }

        private static List<ProtectionRiskItem> BuildProtectionRiskScores(IReadOnlyList<EventSnapshot> events)
        {
            return events
                .Select(e =>
                {
                    var score = 0d;
                    if (!e.IsProtected)
                    {
                        score += 40;
                    }

                    if (e.Year <= 0)
                    {
                        score += 20;
                    }

                    if (!e.Latitude.HasValue || !e.Longitude.HasValue)
                    {
                        score += 20;
                    }

                    if (e.Description.Length < 90)
                    {
                        score += 20;
                    }

                    return new ProtectionRiskItem
                    {
                        EventId = e.Id,
                        Title = e.Title,
                        County = e.InferredCounty,
                        Score = Math.Clamp(score, 0, 100)
                    };
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Title)
                .Take(12)
                .ToList();
        }

        private static List<UncertaintyItem> BuildUncertaintyScores(IReadOnlyList<EventSnapshot> events)
        {
            return events
                .Select(e =>
                {
                    var missingYear = e.Year <= 0 ? 1 : 0;
                    var missingCoord = (!e.Latitude.HasValue || !e.Longitude.HasValue) ? 1 : 0;
                    var sparseNarrative = e.Description.Length < 90 ? 1 : 0;

                    var score = (missingYear * 40) + (missingCoord * 35) + (sparseNarrative * 25);

                    return new UncertaintyItem
                    {
                        EventId = e.Id,
                        Title = e.Title,
                        UncertaintyScore = score,
                        ReasonSummary = $"Year:{(missingYear == 1 ? "missing" : "ok")}, Coords:{(missingCoord == 1 ? "missing" : "ok")}, Narrative:{(sparseNarrative == 1 ? "sparse" : "ok")}"
                    };
                })
                .OrderByDescending(x => x.UncertaintyScore)
                .ThenBy(x => x.Title)
                .Take(12)
                .ToList();
        }

        private static List<OutlierItem> BuildOutliers(IReadOnlyList<EventSnapshot> events, NearestNeighbourStats nearestNeighbour)
        {
            var coords = events.Where(e => e.Latitude.HasValue && e.Longitude.HasValue).ToList();
            if (coords.Count < 3 || nearestNeighbour.ObservedMeanDistanceKm <= 0)
            {
                return [];
            }

            var threshold = nearestNeighbour.ObservedMeanDistanceKm * 2.2;

            return coords
                .Select(e => new
                {
                    Event = e,
                    NearestDistanceKm = coords
                        .Where(other => other.Id != e.Id)
                        .Select(other => HaversineDistanceKm(e.Latitude!.Value, e.Longitude!.Value, other.Latitude!.Value, other.Longitude!.Value))
                        .DefaultIfEmpty(0)
                        .Min()
                })
                .Where(x => x.NearestDistanceKm >= threshold)
                .OrderByDescending(x => x.NearestDistanceKm)
                .Take(10)
                .Select(x => new OutlierItem
                {
                    EventId = x.Event.Id,
                    Title = x.Event.Title,
                    County = x.Event.InferredCounty,
                    NearestDistanceKm = x.NearestDistanceKm,
                    OutlierType = "Geospatially isolated"
                })
                .ToList();
        }

        private static List<KeywordCoOccurrence> BuildKeywordCoOccurrences(IReadOnlyList<EventSnapshot> events)
        {
            var pairs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var ev in events)
            {
                var terms = TokenizeDescription(ev.Description)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .OrderBy(t => t)
                    .ToList();

                for (var i = 0; i < terms.Count; i++)
                {
                    for (var j = i + 1; j < terms.Count; j++)
                    {
                        var key = $"{terms[i]}|{terms[j]}";
                        pairs[key] = pairs.TryGetValue(key, out var value) ? value + 1 : 1;
                    }
                }
            }

            return pairs
                .Where(kvp => kvp.Value >= 2)
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .Take(12)
                .Select(kvp =>
                {
                    var split = kvp.Key.Split('|');
                    return new KeywordCoOccurrence
                    {
                        TermA = split[0],
                        TermB = split[1],
                        CoOccurrenceCount = kvp.Value
                    };
                })
                .ToList();
        }

        private static List<AlignmentConfidenceItem> BuildAlignmentConfidence(IReadOnlyList<EventSnapshot> events)
        {
            return BuildAlignmentClusters(events, 3, 1.8, 10)
                .Select(c =>
                {
                    var spanKm = 0d;
                    if (c.Events.Count >= 2)
                    {
                        var pair = FindFarthestPair(c.Events.Where(e => e.Latitude.HasValue && e.Longitude.HasValue).ToList());
                        spanKm = pair.DistanceKm;
                    }

                    var confidence = Math.Clamp((c.Events.Count * 14) + (spanKm * 1.4), 0, 100);
                    return new AlignmentConfidenceItem
                    {
                        ClusterName = c.DisplayName,
                        County = c.InferredCounty,
                        Records = c.Events.Count,
                        SpanKm = spanKm,
                        ConfidenceScore = confidence
                    };
                })
                .OrderByDescending(x => x.ConfidenceScore)
                .ThenByDescending(x => x.Records)
                .Take(8)
                .ToList();
        }

        private static List<SourceQualityBand> BuildSourceQualityBands(IReadOnlyList<EventSnapshot> events)
        {
            var high = events.Where(e => e.Description.Length >= 180 && e.Year > 0 && e.Latitude.HasValue && e.Longitude.HasValue).ToList();
            var medium = events.Except(high).Where(e => e.Description.Length >= 90 || (e.Year > 0 && e.Latitude.HasValue && e.Longitude.HasValue)).ToList();
            var low = events.Except(high).Except(medium).ToList();

            return
            [
                new SourceQualityBand { Band = "High evidence proxy", RecordCount = high.Count, Notes = "Has precise year + coordinates + richer narrative" },
                new SourceQualityBand { Band = "Medium evidence proxy", RecordCount = medium.Count, Notes = "Partial precision across chronology/location/detail" },
                new SourceQualityBand { Band = "Low evidence proxy", RecordCount = low.Count, Notes = "Sparse narrative or missing chronology/location precision" }
            ];
        }

        private static List<CompletenessEra> BuildCompletenessByEra(IReadOnlyList<EventSnapshot> events)
        {
            return events
                .Where(e => e.Year > 0)
                .GroupBy(e => (e.Year / 50) * 50)
                .OrderBy(g => g.Key)
                .Select(g => new CompletenessEra
                {
                    EraLabel = $"{g.Key}-{g.Key + 49}",
                    Records = g.Count(),
                    YearKnownRate = 1,
                    CoordinateKnownRate = g.Count() == 0 ? 0 : (double)g.Count(e => e.Latitude.HasValue && e.Longitude.HasValue) / g.Count(),
                    NarrativeRichRate = g.Count() == 0 ? 0 : (double)g.Count(e => e.Description.Length >= 120) / g.Count()
                })
                .Take(12)
                .ToList();
        }

        private static bool ContainsAnyKeyword(string description, IEnumerable<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return false;
            }

            var lower = description.ToLowerInvariant();
            return keywords.Any(k => lower.Contains(k, StringComparison.Ordinal));
        }

        private static IEnumerable<string> TokenizeDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return [];
            }

            var cleaned = new string(description
                .ToLowerInvariant()
                .Select(c => char.IsLetter(c) || char.IsWhiteSpace(c) ? c : ' ')
                .ToArray());

            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "the", "and", "for", "that", "with", "from", "into", "this", "was", "were", "have", "has", "had", "near", "stone", "event", "about", "their", "there", "which", "also", "been", "over", "then", "than", "when", "where"
            };

            return cleaned
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => token.Length >= 4)
                .Where(token => !stopWords.Contains(token));
        }

        private static readonly string BrandPrimary = Colors.Blue.Darken2;
        private static readonly string BrandAccent = Colors.Blue.Medium;

        private static void ConfigurePage(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.Margin(1.7f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(10.5f).FontColor(Colors.Grey.Darken4));
            page.Background().Background(Colors.White);
        }

        private static void ComposeStandardHeader(IContainer container, string sectionTitle)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text("The Murder Stone Archive").FontSize(16).SemiBold().FontColor(BrandPrimary);
                        left.Item().Text(sectionTitle).FontSize(11).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(120).AlignRight().Text(DateTime.UtcNow.ToString("yyyy-MM-dd")).FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            });
        }

        private static void ComposeStandardFooter(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                col.Item().PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text("theMurderStoneArchive.com").FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.ConstantItem(130).AlignRight().Text(text =>
                    {
                        text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        text.CurrentPageNumber().FontSize(8).SemiBold();
                        text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        text.TotalPages().FontSize(8).SemiBold();
                    });
                });
            });
        }

        private static IContainer Card(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background(Colors.Grey.Lighten5)
                .Padding(10);
        }

        private static IContainer MetricCard(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background(Colors.Blue.Lighten5)
                .Padding(8);
        }

        private static IContainer TableHeaderCell(IContainer container)
        {
            return container
                .Background(BrandAccent)
                .PaddingVertical(6)
                .PaddingHorizontal(6)
                .DefaultTextStyle(x => x.FontColor(Colors.White).SemiBold())
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten2);
        }

        private static IContainer TableBodyCell(IContainer container)
        {
            return container
                .PaddingVertical(5)
                .PaddingHorizontal(6)
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3);
        }

        private static void BarRow(IContainer container, string label, int count, int maxCount)
        {
            var proportion = maxCount <= 0 ? 0f : (float)count / maxCount;
            var fillPercent = Math.Clamp(proportion, 0f, 1f) * 100f;

            container.Row(row =>
            {
                row.RelativeItem(2).Text(label).FontSize(9.5f);
                row.RelativeItem(5).Column(col =>
                {
                    col.Item().Background(Colors.Grey.Lighten3).Height(8).Row(r =>
                    {
                        r.RelativeItem(fillPercent).Background(BrandPrimary);
                        r.RelativeItem(100f - fillPercent);
                    });
                });
                row.RelativeItem(1).AlignRight().Text(count.ToString()).FontSize(9.5f).SemiBold();
            });
        }

        private static IContainer HeatMapCell(IContainer container, int count, int maxCount)
        {
            if (count <= 0 || maxCount <= 0)
            {
                return container
                    .Background(Colors.Grey.Lighten4)
                    .PaddingVertical(4)
                    .PaddingHorizontal(2)
                    .BorderBottom(1)
                    .BorderColor(Colors.Grey.Lighten3);
            }

            var ratio = (double)count / maxCount;
            var color = ratio switch
            {
                >= 0.75 => Colors.Blue.Darken2,
                >= 0.5 => Colors.Blue.Medium,
                >= 0.25 => Colors.Blue.Lighten2,
                _ => Colors.Blue.Lighten4
            };

            return container
                .Background(color)
                .PaddingVertical(4)
                .PaddingHorizontal(2)
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten3)
                .DefaultTextStyle(x => x.FontColor(ratio >= 0.5 ? Colors.White : Colors.Grey.Darken3).SemiBold());
        }

        private static MapGrid BuildGeoGrid(IReadOnlyList<EventSnapshot> events)
        {
            const int rows = 5;
            const int columns = 6;

            var coordinates = events
                .Where(e => e.Latitude.HasValue && e.Longitude.HasValue)
                .Select(e => (Lat: e.Latitude!.Value, Lon: e.Longitude!.Value))
                .ToList();

            if (coordinates.Count == 0)
            {
                return new MapGrid
                {
                    ColumnLabels = [],
                    Rows = [],
                    MaxCount = 0,
                    TotalPoints = 0
                };
            }

            var minLat = coordinates.Min(c => c.Lat);
            var maxLat = coordinates.Max(c => c.Lat);
            var minLon = coordinates.Min(c => c.Lon);
            var maxLon = coordinates.Max(c => c.Lon);

            if (Math.Abs(maxLat - minLat) < 0.0001)
            {
                maxLat += 0.05;
                minLat -= 0.05;
            }

            if (Math.Abs(maxLon - minLon) < 0.0001)
            {
                maxLon += 0.05;
                minLon -= 0.05;
            }

            var latRange = maxLat - minLat;
            var lonRange = maxLon - minLon;

            var matrix = new int[rows, columns];
            foreach (var point in coordinates)
            {
                var latPos = (maxLat - point.Lat) / latRange;
                var lonPos = (point.Lon - minLon) / lonRange;

                var row = Math.Clamp((int)Math.Floor(latPos * rows), 0, rows - 1);
                var col = Math.Clamp((int)Math.Floor(lonPos * columns), 0, columns - 1);
                matrix[row, col]++;
            }

            var colLabels = Enumerable.Range(0, columns)
                .Select(i =>
                {
                    var start = minLon + (lonRange / columns) * i;
                    var end = minLon + (lonRange / columns) * (i + 1);
                    return $"{start:0.0}–{end:0.0}";
                })
                .ToList();

            var rowModels = new List<MapGridRow>(rows);
            for (var r = 0; r < rows; r++)
            {
                var north = maxLat - (latRange / rows) * r;
                var south = maxLat - (latRange / rows) * (r + 1);

                var rowValues = new List<int>(columns);
                for (var c = 0; c < columns; c++)
                {
                    rowValues.Add(matrix[r, c]);
                }

                rowModels.Add(new MapGridRow
                {
                    Label = $"{north:0.0}–{south:0.0}",
                    Cells = rowValues
                });
            }

            var maxCount = rowModels.SelectMany(r => r.Cells).DefaultIfEmpty(0).Max();

            return new MapGrid
            {
                ColumnLabels = colLabels,
                Rows = rowModels,
                MaxCount = maxCount,
                TotalPoints = coordinates.Count
            };
        }

        private static string FormatYear(int year)
        {
            return year > 0 ? year.ToString() : "Unknown";
        }

        private sealed class EventSnapshot
        {
            public int Id { get; init; }

            public required string Title { get; init; }

            public required string Description { get; init; }

            public int Year { get; init; }

            public required string LocationName { get; init; }

            public string NormalizedLocationName { get; set; } = "unknown";

            public string InferredCounty { get; set; } = "Unknown";

            public double? Latitude { get; init; }

            public double? Longitude { get; init; }

            public bool IsProtected { get; init; }

            public bool IsLost { get; init; }
        }

        private sealed class LocationCluster
        {
            public required string NormalizedName { get; init; }

            public required string DisplayName { get; set; }

            public required string ClusterType { get; init; }

            public required string PatternEvidence { get; set; }

            public string InferredCounty { get; set; } = "Unknown";

            public double? CentroidLatitude { get; set; }

            public double? CentroidLongitude { get; set; }

            public List<EventSnapshot> Events { get; } = new();
        }

        private enum PdfDocumentType
        {
            ProjectBrief,
            ResearchPackOverview,
            ResearchPackTimeline,
            ResearchPackNotes
        }

        private static readonly Dictionary<string, string> CountyKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["somerset"] = "Somerset",
            ["devon"] = "Devon",
            ["dorset"] = "Dorset",
            ["cornwall"] = "Cornwall",
            ["wiltshire"] = "Wiltshire",
            ["hampshire"] = "Hampshire",
            ["surrey"] = "Surrey",
            ["kent"] = "Kent",
            ["sussex"] = "Sussex",
            ["essex"] = "Essex",
            ["london"] = "Greater London",
            ["yorkshire"] = "Yorkshire",
            ["lancashire"] = "Lancashire"
        };

        private static readonly Dictionary<string, (double Latitude, double Longitude)> CountyCentroids = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Somerset"] = (51.1051, -2.9262),
            ["Devon"] = (50.7184, -3.5339),
            ["Dorset"] = (50.7488, -2.3445),
            ["Cornwall"] = (50.2660, -5.0527),
            ["Wiltshire"] = (51.3492, -1.9927),
            ["Hampshire"] = (51.0577, -1.3081),
            ["Surrey"] = (51.2362, -0.5704),
            ["Kent"] = (51.2787, 0.5217),
            ["Sussex"] = (50.9086, -0.4822),
            ["Essex"] = (51.7356, 0.4685),
            ["Greater London"] = (51.5072, -0.1276),
            ["Yorkshire"] = (53.9915, -1.5412),
            ["Lancashire"] = (53.7632, -2.7044)
        };

        private static readonly Dictionary<string, string[]> TrendKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Interpersonal violence"] = ["murder", "killed", "assault", "attack", "stab", "shot", "struck"],
            ["Legal and punishment context"] = ["trial", "court", "executed", "gallows", "verdict", "convicted"],
            ["Folklore and oral history"] = ["legend", "folklore", "ghost", "oral tradition", "myth"],
            ["Religious or ritual framing"] = ["church", "chapel", "holy", "ritual", "saint", "pilgrim"],
            ["Landscape and boundary cues"] = ["boundary", "marker", "crossroads", "moor", "common", "stone"]
        };

        private sealed class CachedPdfDocument
        {
            public required string Fingerprint { get; init; }

            public required byte[] Bytes { get; init; }
        }

        private sealed class TrendAnalysis
        {
            public required List<TrendTheme> Themes { get; init; }

            public required List<TrendDecade> DecadeSeries { get; init; }

            public required List<TrendTerm> TopTerms { get; init; }
        }

        private sealed class TrendTheme
        {
            public required string Name { get; init; }

            public int MatchCount { get; init; }
        }

        private sealed class TrendDecade
        {
            public int DecadeStartYear { get; init; }

            public int EventCount { get; init; }
        }

        private sealed class TrendTerm
        {
            public required string Term { get; init; }

            public int Count { get; init; }
        }

        private sealed class DeepAnalysisResult
        {
            public required List<TemporalAnomaly> TemporalAnomalies { get; init; }

            public required List<CountyShift> SpatioTemporalCountyShifts { get; init; }

            public required NearestNeighbourStats NearestNeighbour { get; init; }

            public required List<NarrativeEraProfile> NarrativeEvolutionByEra { get; init; }

            public required List<CountyProfile> CountyProfiles { get; init; }

            public required List<ProtectionRiskItem> ProtectionRiskScores { get; init; }

            public required List<UncertaintyItem> UncertaintyScores { get; init; }

            public required List<OutlierItem> Outliers { get; init; }

            public required List<KeywordCoOccurrence> KeywordCoOccurrences { get; init; }

            public required List<SourceQualityBand> SourceQualityBands { get; init; }

            public required List<AlignmentConfidenceItem> AlignmentConfidence { get; init; }

            public required List<CompletenessEra> CompletenessByEra { get; init; }
        }

        private sealed class TemporalAnomaly
        {
            public int DecadeStartYear { get; init; }

            public int EventCount { get; init; }

            public double ZScore { get; init; }
        }

        private sealed class CountyShift
        {
            public required string County { get; init; }

            public int TotalRecords { get; init; }

            public int EarlyPeriodRecords { get; init; }

            public int LatePeriodRecords { get; init; }

            public double MeanYearShift { get; init; }
        }

        private sealed class NearestNeighbourStats
        {
            public int PointCount { get; init; }

            public double ObservedMeanDistanceKm { get; init; }

            public double ExpectedMeanDistanceKm { get; init; }

            public double RatioR { get; init; }

            public double ZScore { get; init; }
        }

        private sealed class NarrativeEraProfile
        {
            public required string Era { get; init; }

            public int Records { get; init; }

            public required string TopTheme { get; init; }

            public required string TopTermsSummary { get; init; }
        }

        private sealed class CountyProfile
        {
            public required string County { get; init; }

            public int Records { get; init; }

            public double ProtectedRate { get; init; }

            public int DecadeSpread { get; init; }

            public double ThemeDensity { get; init; }
        }

        private sealed class ProtectionRiskItem
        {
            public int EventId { get; init; }

            public required string Title { get; init; }

            public required string County { get; init; }

            public double Score { get; init; }
        }

        private sealed class UncertaintyItem
        {
            public int EventId { get; init; }

            public required string Title { get; init; }

            public int UncertaintyScore { get; init; }

            public required string ReasonSummary { get; init; }
        }

        private sealed class OutlierItem
        {
            public int EventId { get; init; }

            public required string Title { get; init; }

            public required string County { get; init; }

            public required string OutlierType { get; init; }

            public double NearestDistanceKm { get; init; }
        }

        private sealed class KeywordCoOccurrence
        {
            public required string TermA { get; init; }

            public required string TermB { get; init; }

            public int CoOccurrenceCount { get; init; }
        }

        private sealed class SourceQualityBand
        {
            public required string Band { get; init; }

            public int RecordCount { get; init; }

            public required string Notes { get; init; }
        }

        private sealed class AlignmentConfidenceItem
        {
            public required string ClusterName { get; init; }

            public required string County { get; init; }

            public int Records { get; init; }

            public double SpanKm { get; init; }

            public double ConfidenceScore { get; init; }
        }

        private sealed class CompletenessEra
        {
            public required string EraLabel { get; init; }

            public int Records { get; init; }

            public double YearKnownRate { get; init; }

            public double CoordinateKnownRate { get; init; }

            public double NarrativeRichRate { get; init; }
        }

        private sealed class MapGrid
        {
            public required List<string> ColumnLabels { get; init; }

            public required List<MapGridRow> Rows { get; init; }

            public int MaxCount { get; init; }

            public int TotalPoints { get; init; }
        }

        private sealed class MapGridRow
        {
            public required string Label { get; init; }

            public required List<int> Cells { get; init; }
        }
    }
}
