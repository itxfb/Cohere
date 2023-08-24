using System.Text.Json.Serialization;

namespace Cohere.Entity.Enums.Contribution
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ContributionLanguageCodes
    {
        En = 0, // English

        Vi = 1, // Vietnamese

        Zh = 2, // Chinese

        Es = 3, // Spanish

        Pt = 4, // Portuguese

        Ru = 5, // Russian

        Ar = 6, // Arabic

        Fr = 7, // French

        De = 8, // German

        Hi = 9, // Hindi

        It = 10, // Italian

        Ja = 11, // Japanese

        Ko = 12, // Korean

        Id = 13, // Indonesian

        Sv = 14, // Swedish

        Fi = 15, //Finnish

        Da = 16, //Danish

        Pl = 17  //Polish

    }
}
