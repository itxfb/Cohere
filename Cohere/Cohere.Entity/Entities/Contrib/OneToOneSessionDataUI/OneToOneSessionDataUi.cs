using System;
using System.Collections.Generic;
using Cohere.Entity.Infrastructure.Serializers;
using MongoDB.Bson.Serialization.Attributes;

namespace Cohere.Entity.Entities.Contrib.OneToOneSessionDataUI
{
    public class OneToOneSessionDataUi
    {
        public DateTime StartDay { get; set; }

        public DateTime EndDay { get; set; }

        [BsonSerializer(typeof(BsonStringNumericSerializer))]
        public string Duration { get; set; }

        public int SessionDuration { get; set; }

        public List<SelectedWeek> SelectedWeeks { get; set; } = new List<SelectedWeek>();
    }
}
