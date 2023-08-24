using System;
using System.Collections.Generic;
using System.Text;

namespace Cohere.Entity.Infrastructure.Options
{
    public class ClientUrlsSettings
    {
        public string WebAppUrl { get; set; }

        public string SignUpPath { get; set; }

        public string DashboardPath { get; set; }

        public string ContributionView { get; set; }

        public string SessionBillinglUrl { get; set; }

        public string CoachSessionBillingUrl { get; set; }

        public string AffiliateLinkTemplate { get; set; }
    }
}
