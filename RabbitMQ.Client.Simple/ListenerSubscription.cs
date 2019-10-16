using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Simple
{
    public class ListenerSubscription<T> : RabbitMQSubscription, IRabbitMQSubscription
    {
        public event Action Unsubscribed;

        

        public Action<string> DebugText;

        public event Action<T> Callback;


        private IModel channel;
        private IRabbitMQBodySerializer bodySerializer;

        public ListenerSubscription()
        {

        }

        private async Task createChannel(IConnection connection, CancellationToken stoppingToken)
        {
            while (channel == null && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!connection.IsOpen)
                        await Task.Delay(500, stoppingToken);

                    channel = connection.CreateModel();
                }
                catch
                {

                }
            }
        }

        private void shutdownProcedure()
        {
            if (channel != null)
            {
                try
                {
                    if (channel.IsOpen)
                        channel.Close();
                    channel.Dispose();
                }
                catch
                {

                }
            }
        }

        private void handleReceivedMessage(object sender, BasicDeliverEventArgs e)
        {
            try
            {
                var msgObj = bodySerializer.GetObject<T>(e.Body);
                if (msgObj == null)
                {
                    DebugText?.Invoke("RPCSubscription Callback Failed to parse received message to type " + typeof(T).Name + " using BodySerializer: " + bodySerializer.GetType().Name);
                    return;
                }

                try
                {
                    Callback?.Invoke(msgObj);
                    if (!AutoAck)
                        channel.BasicAck(e.DeliveryTag, false);
                }
                catch (Exception err)
                {
                    DebugText?.Invoke("RPCSubscription Callback Failed to execute Callback -> " + err.ToString());

                    if (!AutoAck)
                        channel.BasicNack(e.DeliveryTag, false, AutoRequeue);
                }
            }
            catch (Exception err)
            {
                DebugText.Invoke("RPCSubscription Callback Failed to handle received message -> " + err.ToString());
            }
        }

        public void Unsubscribe()
        {
            try
            {
                shutdownProcedure();
                Unsubscribed?.Invoke();
            }
            catch
            {

            }
        }

        public void Subscribe(IConnection connection, IRabbitMQBodySerializer serializer, CancellationToken stoppingToken)
        {
            if (connection == null)
                throw new ArgumentNullException();

            if (serializer == null)
                throw new ArgumentNullException();

            this.bodySerializer = serializer;

            Task.Run(async () =>
            {
                await createChannel(connection, stoppingToken);

                stoppingToken.Register(shutdownProcedure);

                channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                var queueName = channel.QueueDeclare(QueueName, Durable, Exclusive, AutoDelete);

                var messageConsumer = new EventingBasicConsumer(channel);

                messageConsumer.Received += handleReceivedMessage;

                channel.BasicConsume(
                    queue: queueName,
                    autoAck: AutoAck,
                    consumer: messageConsumer
                    );

                if (Exchange.Equals("") || RoutingKey.Equals(""))
                    return;

                channel.QueueBind(
                    queue: queueName,
                    routingKey: RoutingKey,
                    exchange: Exchange,
                    arguments: null
                    );
            });
        }
    }
}
