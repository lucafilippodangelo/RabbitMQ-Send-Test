using System;
using System.Text;
using RabbitMQ.Client;

namespace Send
{
    class Send
    {
        public static void Main(string[] args)
        {
            //runVersion(1); //LD STEP001
            //runVersion(2); //LD STEP002
            //runVersion(3); //LD STEP003
            //runVersion(4); //LD STEP004
            runVersion(5); //LD STEP005
        }

        //LD invoke methods
        #region region common methods
        public static void runVersion(int ver)
        {
            string[] vettoreDiStringhe = new string[] { "aaa.......", "bbb...", "ccc.", "ddd.....", "eee............", "fff..", "ggg............", "hhh...", "iii." };

            string[] stringa = new string[] { "aaa-green", "bbb-orange", "ccc-red", "ddd-red", "eee-red", "fff-orange", "ggg-green.", "hhh-orange", "iii-green" };
            string[] severity = new string[] {"green", "orange", "red", "red", "red", "orange", "green", "orange", "green"};
            string[] topicExchangeRoutingKey = new string[] { "facilityA.green", "facilityB.orange", "facilityC.red", "facilityD.red", "facilityE.red", "facilityF.orange", "facilityG.green", "facilityH.orange", "facilityI.green" };

            switch (ver)
            {
                case 1:
                    sendVersion1();
                    break;
                case 2:
                    foreach (string element in vettoreDiStringhe)
                    {
                        sendVersion2(element);
                    }
                    break;
                case 3:
                    foreach (string element in vettoreDiStringhe)
                    {
                        sendVersion3(element);
                    }
                    break;
                case 4:
                    for (int i = 0; i < stringa.Length; i++)
                    {
                        sendVersion4(stringa[i],severity[i]);
                    }
                    break;
                case 5:
                    for (int i = 0; i < stringa.Length; i++)
                    {
                        sendVersion5(stringa[i], topicExchangeRoutingKey[i]);
                    }
                    break;
                default:
                    Console.WriteLine("Default case");
                    break;
            }

            Console.WriteLine(" task_queue - Press [enter] to exit.");
            Console.ReadLine();
        }
        #endregion

        #region region //LD STEP001
        private static void sendVersion1()
        {
            // The connection abstracts the socket connection, and takes care of protocol version negotiation 
            // and authentication and so on for us. Here we connect to a broker on the local machine 
            // hence the localhost. If we wanted to connect to a broker on a different machine we'd simply 
            // specify its name or IP address here.
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())

            // create a channel, which is where most of the API for getting things done resides
            using (var channel = connection.CreateModel())
            {
                //To send, we must declare a queue for us to send to, will only be created if it doesn't exist already. 
                channel.QueueDeclare(queue: "hello", durable: false, exclusive: false, autoDelete: false, arguments: null);

                //The message content is a byte array, so you can encode whatever you like there.
                string message = "Hello World! " + DateTime.Now.ToString();
                var body = Encoding.UTF8.GetBytes(message);

                //then we can publish a message to the queue
                channel.BasicPublish(exchange: "", routingKey: "hello", basicProperties: null, body: body);
                Console.WriteLine(" [x] Sent {0}", message);
            }

            Console.WriteLine(" Press [enter] to exit.");
            Console.ReadLine();
        }
        #endregion

        #region region //LD STEP002
        /// <summary>
        /// We will slightly modify the Send program from //LD STEP001, to allow arbitrary messages 
        /// to be sent from the command line. This program will schedule tasks to our work queue, so let's name it NewTask:
        /// </summary>
        //LD STEP002
        private static void sendVersion2(string anElement)


        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "task_queue",
                                     durable: true, //LD STEP002B
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                var message = anElement;//GetMessage(args);
                var body = Encoding.UTF8.GetBytes(message);

                var properties = channel.CreateBasicProperties();
                //LD STEP002C
                properties.Persistent = true; 

                channel.BasicPublish(exchange: "",//LD STEP003A
                                     routingKey: "task_queue",//LD STEP003A
                                     basicProperties: properties,
                                     body: body);

                //LD removed to automatize the sending of the messages by a for loop
                //Console.WriteLine(" [x] Sent {0}", message);
            }
        }
        #endregion

        #region region //LD STEP003
        /// <summary>
        /// This method emit logs, The producer program, doesn't look much different from the //LD STEP002. 
        /// The most important change is that we now want to publish messages to our logs 
        /// exchange instead of the nameless one.
        /// WE DON'T DECLARE A QUEUE
        /// </summary>
        //LD STEP003
        private static void sendVersion3(string anElement)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {

                //LD STEP003B
                channel.ExchangeDeclare(exchange: "logs", type: "fanout");

                var message = anElement;
                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish(exchange: "logs", //LD STEP003B
                                     routingKey: "", //LD STEP003B
                                     basicProperties: null, //LD not anymore "persistent"
                                     body: body);

                Console.WriteLine(" [x] Sent {0}", message);
            }
        }
        #endregion

        #region region //LD STEP004
        /// <summary>
        /// Adding a feature to //LD STEP003, we're going to make it possible to subscribe only to a subset of the messages.
        /// WE DON'T DECLARE A QUEUE
        /// </summary>
        //LD STEP004
        private static void sendVersion4(string message, string severity)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                //LD STEP004C
                channel.ExchangeDeclare(exchange: "direct_logs", 
                                        type: "direct");

                var body = Encoding.UTF8.GetBytes(message);

                //LD STEP004D
                // a message goes to the queues 
                // whose binding key exactly matches the routing key of the message
                channel.BasicPublish(exchange: "direct_logs",
                                     routingKey: severity,
                                     basicProperties: null,
                                     body: body);

                Console.WriteLine(" [x] Sent '{0}':'{1}'", severity, message);
            }
        }
        #endregion

        #region region //LD STEP005
        /// <summary>
        /// Use of Topic Exchange. Implementation of updates to the code in order to subscribe
        /// to not only logs based on severity, but also based on the source which emitted the log.
        /// </summary>
        //LD STEP005
        private static void sendVersion5(string message, string topicExchangeRoutingKey)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                //LD STEP005B
                channel.ExchangeDeclare(exchange: "topic_logs",
                                        type: "topic");

                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish(exchange: "topic_logs",
                                                 routingKey: topicExchangeRoutingKey,
                                                 basicProperties: null,
                                                 body: body);

                Console.WriteLine(" [x] Sent for ver.5 '{0}':'{1}'", topicExchangeRoutingKey, message);
            }
        }
        #endregion

    }
}
