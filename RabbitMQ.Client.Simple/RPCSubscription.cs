using Newtonsoft.Json;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RabbitMQ.Client.Simple
{
    public class RPCSubscription<T> : RabbitMQSubscription, IRabbitMQSubscription
    {


        public Action<string> DebugText;

        public event Action Unsubscribed;

        public Func<T, object> Callback;


        private IModel channel;
        private IRabbitMQBodySerializer bodySerializer;

        public RPCSubscription()
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
                    if(channel.IsOpen)
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

                object result = null;

                try
                {
                    result = Callback?.Invoke(msgObj);
                }
                catch (Exception err)
                {
                    DebugText?.Invoke("RPCSubscription Callback Failed to execute Callback -> " + err.ToString());

                    if (!AutoAck)
                        channel.BasicNack(e.DeliveryTag, false, AutoRequeue);
                }

                if (result == null)
                {
                    if (!AutoAck)
                        channel.BasicNack(e.DeliveryTag, false, AutoRequeue);

                    DebugText?.Invoke("RPCSubscription Callback Returned null");
                    return;
                }

                if (!AutoAck)
                    channel.BasicAck(e.DeliveryTag, false);

                if (e.BasicProperties == null || e.BasicProperties.ReplyTo == null)
                    return;

                var responseProps = channel.CreateBasicProperties();
                responseProps.CorrelationId = e.BasicProperties.CorrelationId;

                channel.BasicPublish(
                    exchange: "",
                    routingKey: e.BasicProperties.ReplyTo,
                    body: bodySerializer.GetBytes(result),
                    basicProperties: responseProps
                );

            }
            catch (Exception err)
            {
                DebugText.Invoke("RPCSubscription Callback Failed to handle received message -> "+err.ToString());
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

                

                var queueName = channel.QueueDeclare(QueueName, Durable, Exclusive, AutoDelete);

                var messageConsumer = new EventingBasicConsumer(channel);

                messageConsumer.Received += handleReceivedMessage;


                channel.BasicConsume(
                    queue: queueName,
                    autoAck: AutoAck,
                    consumer: messageConsumer
                    );

                channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

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
