using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;


//using Messages;
using ChatLib;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
namespace ChatServer
{
    class Client
    {   
        public Thread Thread { get; private set; }
        public TcpClient MyTcpClient { get; private set; }
        public int Id { get; private set; }
        public Client() { }
        public Client(Thread thread, TcpClient tcpClient, int id)
        {
            Thread = thread;
            MyTcpClient = tcpClient;
            Id = id;
        }
    }

    class Program
    {
        // Идентификатор клиента        
        static int globalClientsId = 0;
        // Коллекция клиентов
        static SortedList<int, Client> clients = new SortedList<int, Client>();
        static object locker = new object();
        /// <summary>
        /// Обработка запросов
        /// </summary>
        /// <param name="clientId"></param>
        static void requestsProcessing(int clientId)
        {            
            IFormatter formatter = new BinaryFormatter();
            // Поток клиента
            NetworkStream currentClientStream = clients[clientId].MyTcpClient.GetStream();
            while(true)
            { 
                // Получить запрос
                object request = formatter.Deserialize(currentClientStream);
                switch ((int)request)
                {
                    // Клиент прислал сообщение для других пользователей
                    case (int)Requests.NewMessage:
                        // Принять сообщение
                        UsualMessage message = acceptClientMessage(clientId);
                        string msgToLog = message.Sender + ": " + message.Message;
                        LoggerEvs.writeLog(msgToLog);
                        // Разослать сообщение всем клиентам                    
                        sendMessageToClients(msgToLog);
                        break;
                    // Клиент прислал запрос на закрытие соединения
                    case (int)Requests.CloseConnection:
                        lock (locker)
                        {
                            // Закрыть соединение с клиентом
                            clients[clientId].MyTcpClient.Close();
                            // Удалить клиента из коллекции                             
                            clients.Remove(clientId);
                            // Оповестить клиентов об отключение данного
                            string closeConnectionMessage = "Client " + clientId + " has just been disconnected.";
                            sendMessageToClients(closeConnectionMessage);
                            LoggerEvs.writeLog(closeConnectionMessage);
                            // Завершать выполнение потока
                            return;
                        }                       
                }
            }
        }
        

        /// <summary>
        /// /// Принять сообщение от клиента
        /// </summary>
        /// <param name="clientId">Идентификатор клиента</param>
        /// <returns>Сообщение с сервера</returns>
        static UsualMessage acceptClientMessage(int clientId)
        {
            IFormatter formatter = new BinaryFormatter();
            NetworkStream stream = clients[clientId].MyTcpClient.GetStream();
            return (UsualMessage)formatter.Deserialize(stream);
        }


        /// <summary>
        /// Разослать сообщения клиентам
        /// </summary>
        /// <param name="message">Сообщение</param>
        static void sendMessageToClients(string message)
        {
            IFormatter formatter = new BinaryFormatter();
            NetworkStream clientStream;
            lock (locker)
            {
                foreach (var client in clients)
                {
                    try
                    {
                        // Получить поток клиента
                        clientStream = client.Value.MyTcpClient.GetStream();
                        // Отослать клиенту сообщение
                        formatter.Serialize(clientStream, message);
                    }
                    catch (Exception ex)
                    {
                        LoggerEvs.writeLog(ex.ToString());
                    }
                }
            }
        }

        static void Main(string[] args)
        {            
            LoggerEvs.messageCame += logToConsole;
            /////////////////////////////////
            // Получить настройки сервера
            ServerProperties serverProps = new ServerProperties();
            serverProps.ReadXML();                        
            String propsMsg = String.Format("Настройки сервера: ServerName = {0}; IP = {1}; Port = {2};", serverProps.Fields.ServerName, IPAddress.Any.ToString(), serverProps.Fields.Port);
            LoggerEvs.writeLog(propsMsg);
            Console.Title = serverProps.Fields.ServerName;
            // Создать tcp-слушатель            
            TcpListener myTcpListener = new TcpListener(IPAddress.Any, serverProps.Fields.Port);
            // Запустить слушатель
            myTcpListener.Start();
            // Текущий подключенный клиент
            TcpClient client;
            // Текущий поток клиента
            Thread clientTh;           

            while(true)
            {
                // Принять новое подключение
                client = myTcpListener.AcceptTcpClient();
                LoggerEvs.writeLog("New client connected.");
                lock (locker) 
                {
                    int id = globalClientsId;                    
                    clientTh = new Thread(delegate() { requestsProcessing(id); });
                    Client newClient = new Client(clientTh, client, globalClientsId);
                    // Добавить клиента в коллекцию
                    clients.Add(id, newClient);                    
                    // Создать новый поток
                    clients[id].Thread.Start();                    
                    globalClientsId++;                    
                }
            }            
        }
        
        /// <summary>
        /// Внести запись в лог
        /// </summary>
        /// <param name="logNote">Запись в лог</param>
        static void logToConsole(String logNote)
        {
            Console.Write(logNote);
        }

        
    }
}
