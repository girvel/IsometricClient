﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CommandInterface;
using UnityEngine;
using Assets.Code.Tools;
using Assets.Code.Interface;
using Assets.Code.Game;
using Assets.Code.Interface.Game;
using Assets.Code.Interface.Signin;
using Assets.Code.Tools.Extensions;
using Assets.Code.Tools.Prefabs;
using BinarySerializationExtensions;
using CommandInterface.Extensions;
using CommonStructures;
using SocketExtensions;
using VectorNet;

namespace Assets.Code
{
    public class NetController : MonoBehaviour, IDisposable
    {
        public class NetArgs
        {
            public Socket MainSocket { get; private set; }

            public NetArgs(Socket mainSocket)
            {
                MainSocket = mainSocket;
            }
        }



        public static NetController Instance { get; set; }



        public int ServerPort = 8005;
        public string ServerAddress = "192.168.0.100";

        public int ConnectTimeSeconds = 3;
        public int ConnectDelayMilliseconds = 100;

        public string Email;
        public string Password;

        public bool ConnectionActive;

        public Encoding Encoding = Encoding.GetEncoding(1251);



        public event Action OnConnectionFail;
        public event Action OnConnectionSuccess;



        protected Interface<NetArgs, bool> CommandInterface;

        protected Thread ConnectionThread;
        protected Socket Socket;

        private readonly object _socketLock = new object();



        protected virtual void Start()
        {
            Instance = this;

            #region commands

            CommandInterface = new Interface<NetArgs, bool>(
                new Command<NetArgs, bool>(
                    "login-result", new[] {"result"}, // TODO login-result to unactive
                    _loginResult),

                new Command<NetArgs, bool>(
                    "set-territory", new[] {"territory"},
                    _setTerritory),

                new Command<NetArgs, bool>(
                    "set-building-actions", new[] {"actions"},
                    _setBuildingActions),

                new Command<NetArgs, bool>(
                    "resources", new[] {"resources"},
                    _resources),

                new Command<NetArgs, bool>(
                    "upgrade-result", new[] {"building"},
                    _upgradeResult));

            #endregion

            UiController.BuildingChoosed += SendBuildingActionsRequest;
            UiController.ActionChoosed += SendUpgradeRequest;
        }

        protected virtual void OnDestroy()
        {
            Dispose();
        }



        public void LoginConnect()
        {
            try
            {
                Connection();
                LoginConnection();
                ConnectionThread = new Thread(ConnectionLoopStart);
                ConnectionThread.Start();
            }
            catch (SocketException)
            {
                Debug.Log("Connection problem");
            }
        }

        public void SignUpConnect()
        {
            Connection();
        }

        public void SendEmail(string email)
        {
            Socket.Send(
                "email-send-code".CreateCommand(email),
                Encoding);
        }



        private void Connection()
        {
            ActionsProcessor.AddActionToQueue(() =>
                    Ui.ResourcesLineConnectionStatus.Content = UiController.ConnectionStatusConnection);

            var ipPoint = new IPEndPoint(IPAddress.Parse(ServerAddress), ServerPort);
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            var connected = false;
            var connectBeginTime = DateTime.Now;

            do
            {
                try
                {
                    Socket.Connect(ipPoint);
                    connected = true;
                    break;
                }
                catch (SocketException)
                {
                    Thread.Sleep(ConnectDelayMilliseconds);
                }
            } while ((DateTime.Now - connectBeginTime).Seconds < ConnectTimeSeconds);

            if (!connected)
            {
                ActionsProcessor.AddActionToQueue(() =>
                {
                    if (OnConnectionFail != null)
                    {
                        OnConnectionFail();
                    }
                });

                throw new SocketException();
            }

            ActionsProcessor.AddActionToQueue(() =>
            {
                if (OnConnectionSuccess != null)
                {
                    OnConnectionSuccess();
                }
            });

            ActionsProcessor.AddActionToQueue(() =>
                    Ui.ResourcesLineConnectionStatus.Content = UiController.ConnectionStatusConnected);
        }

        private void LoginConnection() {
            Socket.Send(
                "login".CreateCommand(
                    new CommonAccount(Email, Password).Serialize(Encoding)),
                Encoding);
        }

        private void ConnectionLoopStart()
        {
            try
            {
                ConnectionActive = true;
                while (ConnectionActive)
                {
                    var receivedString = Socket.ReceiveAll(Encoding);

                    ActionsProcessor.AddActionToQueue(() =>
                    {
                        Debug.Log(receivedString);
                        CommandInterface.GetExecutor(receivedString, new NetArgs(Socket))();
                    });
                }

                Socket.Shutdown(SocketShutdown.Both);
                Socket.Close();
                ConnectionActive = false;
            }
            catch (SocketException)
            {
                Debug.LogError("Disconnect!");
                LoginConnect();
            }
            catch (ThreadAbortException)
            {
                ConnectionThread = null;
                Dispose();
            }
        }

        public void Dispose()
        {
            if (Socket != null)
            {
                Socket.Close();
            }

            if (ConnectionThread != null)
            {
                ConnectionThread.Abort();
            }
        }



        private void SendBuildingActionsRequest(string name, Vector2 isometricPosition)
        {
            Socket.Send(
                "get-building-actions".CreateCommand(
                    new CommonBuilding(isometricPosition.ToIntVector())
                        .Serialize(Encoding)),
                Encoding);
        }

        private void SendUpgradeRequest(UiController.BuildingAction action)
        {
            Socket.Send(
                "upgrade".CreateCommand(action.Common.Serialize(Encoding)),
                Encoding);
        }



        // @result
        private bool _loginResult(IDictionary<string, string> args, NetArgs netArgs)
        {
            try
            {
                switch ((LoginResult)int.Parse(args["result"]))
                {
                    case LoginResult.Successful:
                        netArgs.MainSocket.Send("get-territory", Encoding);

                        Ui.LoginStatus.Content = LoginStatus.Succes;

                        LoginButtonController.AuthenticationFormActive = false;

                        Ui.ResourcesLineForm.GameObject.SetActive(true);
                        return true;

                    case LoginResult.Unsuccessful:
                        Ui.LoginStatus.Content =
                            LoginStatus.LoginFail;
                        return true;

                    case LoginResult.Banned:
                        Ui.LoginStatus.Content =
                            LoginStatus.Banned;
                        return true;

                    default:
                        throw new NotImplementedException();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(string.Format("[{0}; message = {1}]", ex.GetType(), ex.Message));
                return true;
            }
        }

        // @territory
		private bool _setTerritory(IDictionary<string, string> args, NetArgs netArgs)
		{
			Debug.Log("Buildings received");

		    var territory = args["territory"].Deserialize<CommonTerritory>(Encoding);

		    BuildingsContainer.Instance.BuildingsGrid
		        = new Building[
		            territory.PatternIDs.GetLength(0),
		            territory.PatternIDs.GetLength(1)];

            for (var y = 0; y < territory.PatternIDs.GetLength(1); y++)
            {
                for (var x = 0; x < territory.PatternIDs.GetLength(0); x++)
                {
                    BuildingsContainer.Instance.CreateBuilding(
                        territory.PatternIDs[x, y],
                        new Vector2(x, y));
                }
            }

            netArgs.MainSocket.Send("get-resources", Encoding);

            return true;
        }

        // @actions
		private bool _setBuildingActions(Dictionary<string, string> args, NetArgs netArgs)
		{
		    var actions = args["actions"].Deserialize<List<CommonBuildingAction>>(Encoding);

            foreach (var action in actions)
            {
                Debug.Log(action.Active + ", " + action.Name);
            }

            UiController.BuildingActions = new ReadOnlyCollection<CommonBuildingAction>(actions);

            return true;
        }

        // @resources
		private bool _resources(Dictionary<string, string> args, NetArgs netArgs)
		{
            var resources =
                args["resources"].Deserialize<CommonStructures.Resources>(Encoding);

            for (var i = 0; i < resources.ResourcesArray.Length; i++)
            {
                Ui.Resources[i].Content = resources.ResourcesArray[i];
            }

            return true;
        }

        // @building
        private bool _upgradeResult(Dictionary<string, string> args, NetArgs netArgs)
        {
            if (args["building"] == "-1")
            {
                return false;
            }

            var result = args["building"].Deserialize<UpgradeResult>(Encoding);

            var building = BuildingsContainer.Instance
                .BuildingsGrid[result.Position.X, result.Position.Y];
            
            building.ChangeBuildingSprite(
                BuildingPrefabsContainer.Instance
                    .Get(BuildingsContainer.Instance.PrefabsNames[result.ID])
                    .BuildingSpriteRenderer.sprite);
            
            return true;
        }
    }
}
