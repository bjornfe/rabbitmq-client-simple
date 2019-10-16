using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RabbitMQ.Client.Simple
{
    public class RabbitMQServiceBuilder
    {
        private RabbitMQOptions options = new RabbitMQOptions();
        private RabbitMQService rmqService;
        private CancellationTokenSource tokenSource;
        private CancellationToken stoppingToken;
        private bool useExternalToken;

        private List<IRabbitMQSubscription> subscriptions = new List<IRabbitMQSubscription>();
        public RabbitMQServiceBuilder()
        {
            rmqService = new RabbitMQService(options);
            tokenSource = new CancellationTokenSource();
            stoppingToken = tokenSource.Token;

            rmqService.Connected += () =>
            {
                foreach(var s in subscriptions)
                {
                    rmqService.AddSubscription(s);
                }
            };
        }

        public RabbitMQServiceBuilder SetDebugWriter(Action<string> act)
        {
            rmqService.DebugText += (txt) => act?.Invoke(txt);
            return this;
        }

        public RabbitMQServiceBuilder SetOptions(Action<RabbitMQOptions> opt)
        {
            opt?.Invoke(options);
            return this;
        }

        public RabbitMQServiceBuilder UseCancellationToken(CancellationToken token)
        {
            this.stoppingToken = token;
            this.tokenSource.Dispose();
            return this;
        }

        public RabbitMQServiceBuilder AddListenerSubscription<T>(Action<ListenerSubscription<T>> subscription)
        {
            if (subscription == null)
                return this;

            var sub = new ListenerSubscription<T>();
            subscription.Invoke(sub);
            subscriptions.Add(sub);
            return this;
        }

        public RabbitMQServiceBuilder AddRpcSubscription<T>(Action<RPCSubscription<T>> subscription)
        {
            if (subscription == null)
                return this;

            var sub = new RPCSubscription<T>();
            subscription.Invoke(sub);
            subscriptions.Add(sub);
            return this;
        }

        public RabbitMQServiceBuilder UseJsonSerializer()
        {
            rmqService.BodySerializer = new RabbitMQJsonSerializer();
            return this;
        }

        public RabbitMQServiceBuilder UseMessagePackSerializer()
        {
            rmqService.BodySerializer = new RabbitMQMessagePackSerializer();
            return this;
        }

        public RabbitMQService Build()
        {
            rmqService.Run(stoppingToken);
            return rmqService;
        }




    }
}
