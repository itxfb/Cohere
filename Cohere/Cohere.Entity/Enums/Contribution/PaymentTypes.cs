using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
namespace Cohere.Entity.Enums.Contribution
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PaymentTypes
    {
        Simple = 0,
        Advance = 1,
    }
}