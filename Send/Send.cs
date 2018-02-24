﻿using System;
using System.Text;
using RabbitMQ.Client;


namespace Send
{
    class Send
    {
        public static void Main(string[] args)
        {
            //LD invoke methods
            #region region //LD STEP001
            //sendVersion1();
            #endregion

            #region region //LD STEP002
            string[] vettoreDiStringhe = new string[] { "aaa.......", "bbb...", "ccc.", "ddd.....", "eee............", "fff..", "ggg............", "hhh...", "iii." };
            foreach (string element in vettoreDiStringhe)
            {
                sendVersion2(element);
            }
            Console.WriteLine(" task_queue - Press [enter] to exit.");
            Console.ReadLine();
            #endregion

        }

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

                channel.BasicPublish(exchange: "",
                                     routingKey: "task_queue",
                                     basicProperties: properties,
                                     body: body);

                //LD removed to automatize the sending of the messages by a for loop
                //Console.WriteLine(" [x] Sent {0}", message);
            }
        }
        #endregion

    }
}