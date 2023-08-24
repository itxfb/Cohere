
namespace Cohere.Domain.Service.FCM.Serialization
{
    public interface ISerializer
    {
        T Deserialize<T>(string json);
        string Serialize<T>(T value);
    }
}
