﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using uPLibrary.Networking.M2Mqtt;

namespace ScaleMessagesManager
{
    public class ScaleManager
    {
        private readonly MqttClient _mqttClient;

        private readonly Action<double> _onWeightReceived;
        private readonly Action<double> _onFinalWeightReceived;

        private List<double> _weightsReceived;
        private double? _lastWeight = null;

        private Stopwatch _weightTimer;
        private bool _hasFinalWeight = false;

        public ScaleManager(
            Action<double> onWeightReceived,
            Action<double> onFinalWeightReceived)
        {
            _mqttClient = new MqttClient(GetLocalIPAddress());
            _onWeightReceived = onWeightReceived;
            _onFinalWeightReceived = onFinalWeightReceived;

            _weightsReceived = new List<double>();
        }

        public void StartListening()
        {
            _mqttClient.MqttMsgPublishReceived += Q_MqttMsgPublishReceived;
            _mqttClient.Subscribe(new string[] { "pf/scale" }, new byte[] { 0x01 });
            _mqttClient.Connect("BonizPc");
        }

        private void Q_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            if (_hasFinalWeight)
                return;

            var messageString = Encoding.Default.GetString(e.Message);
            if (string.IsNullOrWhiteSpace(messageString))
                return;

            var message = JsonConvert.DeserializeObject<ScaleMessage>(messageString);

            _onWeightReceived(message.Weight);

            if (IsTheSameWithTolerance(message.Weight, _lastWeight))
            {
                _weightTimer = new Stopwatch();
                _weightTimer.Start();
            }

            _lastWeight = message.Weight;
            _weightsReceived.Add(message.Weight);

            if (HasFinalWeight())
                _onFinalWeightReceived(message.Weight);
        }

        private bool HasFinalWeight()
        {
            if (_weightTimer == null)
                return false;

            if (_weightTimer.Elapsed >= TimeSpan.FromSeconds(2))
            {
                return true;
            }

            return false;
        }

        private bool IsTheSameWithTolerance(double? a, double? b, double tolerance = 0.05)
        {
            if (a == null || b == null)
                return false;

            return Math.Abs(a.Value - b.Value) <= tolerance;
        }

        public void StopListening()
        {
            _mqttClient.Disconnect();
            _mqttClient.MqttMsgPublishReceived -= Q_MqttMsgPublishReceived;
        }

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}