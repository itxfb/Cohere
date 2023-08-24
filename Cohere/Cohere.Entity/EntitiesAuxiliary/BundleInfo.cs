using Cohere.Entity.Enums;

namespace Cohere.Entity.EntitiesAuxiliary
{
    public class BundleInfo : BaseEntity
    {
        public BundleParentType BundleParentType { get; set; }

        public string ParentId { get; set; }

        public string ItemId { get; set; }
    }
}