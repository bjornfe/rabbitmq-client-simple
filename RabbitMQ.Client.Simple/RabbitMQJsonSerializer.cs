using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.Client.Simple
{
    public class RabbitMQJsonSerializer : IRabbitMQBodySerializer
    {
        public byte[] GetBytes(object obj)
        {
            try
            {
                var json = JsonConvert.SerializeObject(obj);
                return Encoding.UTF8.GetBytes(json);
            }
            catch(Exception err)
            {
                Console.WriteLine("Failed to convert to bytes -> " + err.ToString());
                return null;
            }
        }

        public T GetObject<T>(byte[] body)
        {
            try
            {
                var json = Encoding.UTF8.GetString(body);
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return default(T);
            }
        }
    }
}
