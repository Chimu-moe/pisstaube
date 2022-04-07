
using System.Text.Json;

namespace Pisstaube.Core.Utils
{
    public static class JsonUtil
    {
        public static void Initialize()
        {
            /*
            CompositeResolver.RegisterAndSetAsDefault(new IJsonFormatter[] {
                new DateTimeFormatter("yyyy-MM-ddTHH:mm:ssZ"),
                new NullableDateTimeFormatter("yyyy-MM-ddTHH:mm:ssZ")
            }, new[] {
                EnumResolver.UnderlyingValue,

                StandardResolver.AllowPrivateExcludeNullSnakeCase
            });
            */
        }
        
        public static string Serialize<T>(T obj)
        {
            var serializedData = JsonSerializer.Serialize(
                obj
            );
    
            return serializedData;
        }
        
        public static T Deserialize<T>(string data) where T : class, new()
        {
            var deserializedObject = JsonSerializer.Deserialize<T>(data);
            return deserializedObject;
        }
    }
}