using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cohere.Domain.Models.ContributionViewModels.ForCohealer
{
    public class SelfpacedDetails
    {
        [Name("First Name")]
        [Index(0)]
        public string FirstName { set; get; }

        [Name("Last Name")]
        [Index(1)]
        public string LastName { set; get; }

        [Name("Client Email")]
        [Index(2)]
        public string Email { set; get; }

        
        public List<ModuleAndContent> ModuleContentList{set; get;}
        
    }

    public class ModuleAndContent
    {
        public string ModuleName{ set; get; }

        public string ContentName { set; get; }

        public string Status { set; get; }
    }
}
