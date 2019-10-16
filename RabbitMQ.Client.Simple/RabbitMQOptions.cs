using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitMQ.Client.Simple
{
    public class RabbitMQOptions
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string VirtualHost { get; set; } = "/";
        public string Hostname { get; set; }
        public int Port { get; set; } = 5672;
        public bool UseSSL { get; set; } = false;
    }
}
