using Microsoft.EntityFrameworkCore;
using TheMurderStoneArchive.Models;

namespace TheMurderStoneArchive.Data
{
    /// <summary>
    /// Extension methods for MurderEvent queries to reduce duplication.
    /// </summary>
    public static class MurderEventQueryExtensions
    {
        /// <summary>
        /// Filters MurderEvents to only approved events that are not marked as lost.
        /// This is the standard public-facing filter used across the application.
        /// </summary>
        public static IQueryable<MurderEvent> ApprovedAndNotLost(this IQueryable<MurderEvent> query)
        {
            return query.Where(m => m.IsApproved && !m.IsLost);
        }

        /// <summary>
        /// Includes all related data: Location, Photos, Videos, Monuments, and Perpetrators.
        /// </summary>
        public static IQueryable<MurderEvent> WithAllRelations(this IQueryable<MurderEvent> query)
        {
            return query
                .Include(m => m.Location)
                .Include(m => m.Monuments)
                .Include(m => m.Perpetrators)
                .Include(m => m.Photos)
                .Include(m => m.Videos);
        }

        /// <summary>
        /// Includes only basic related data: Location, Photos, and Videos.
        /// </summary>
        public static IQueryable<MurderEvent> WithBasicRelations(this IQueryable<MurderEvent> query)
        {
            return query
                .Include(m => m.Location)
                .Include(m => m.Photos)
                .Include(m => m.Videos);
        }

        /// <summary>
        /// Includes only the Location relationship.
        /// </summary>
        public static IQueryable<MurderEvent> WithLocation(this IQueryable<MurderEvent> query)
        {
            return query.Include(m => m.Location);
        }
    }
}
