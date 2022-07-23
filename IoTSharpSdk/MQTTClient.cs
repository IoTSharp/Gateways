using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
 
using MQTTnet.Diagnostics;
using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IoTSharp.MqttSdk
{


    public class MqttClientHost : BackgroundService
    {
        private readonly MQTTClient client;

        public MqttClientHost(MQTTClient client)
        {
            this.client = client;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            do
            {
                await Task.Run(async () =>
              {
                  if (!client.IsConnected)
                  {
                      await client.ConnectAsync();
                  }
              });
                await Task.Delay(TimeSpan.FromSeconds(10));
            } while (!stoppingToken.IsCancellationRequested);
        }
    }

    public class MQTTClient
    {
        public MQTTClient(IServiceScopeFactory scopeFactor, IOptions<MqttSettings> options, ILogger<MQTTClient> logger)
        {
            _settings = options.Value;
            BrokerUri = new Uri(_settings.MqttBroker);
            DeviceId = _settings.DeviceId;
            OnReceiveAttributes += MQTTClient_OnReceiveAttributes;
           OnExcRpc += Client_OnExcRpc;
            OnConnected += MQTTClient_OnConnected;
            _logger = logger;
        }

        private void MQTTClient_OnReceiveAttributes(object? sender, AttributeResponse e)
        {
            switch (e.KeyName)
            {
                case "rootuser":

                    break;
                default:
                    break;
            }
        }

        private void MQTTClient_OnConnected(object? sender, MqttClientConnectedEventArgs e)
        {
        
        }

        public async Task DisconnectAsync()
        {
            await Client.DisconnectAsync();
            Client.Dispose();
        }
        private void Client_OnExcRpc(object? sender, RpcRequest e)
        {

        }

        

        public string DeviceId { get; set; } = string.Empty;

        private readonly MqttSettings _settings;
        private readonly ILogger<MQTTClient> _logger;

        public Uri BrokerUri { get; set; }
        public bool IsConnected => (Client?.IsConnected).GetValueOrDefault();
        private IMqttClient Client { get; set; }

        public event EventHandler<RpcRequest> OnExcRpc;

        public event EventHandler<AttributeResponse> OnReceiveAttributes;

        public event EventHandler<MqttClientConnectedEventArgs> OnConnected;

        public async Task<bool> ConnectAsync(Uri uri)
        {
            BrokerUri = uri;
            return await ConnectAsync();
        }
        public async Task<bool> ConnectAsync()
        {
            var uri = BrokerUri;
            bool initok = false;
            string username = "";
            string password = "";
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var uinfo = uri.UserInfo.Split(':');
                if (uinfo.Length > 0) username = uinfo[0];
                if (uinfo.Length > 1) password = uinfo[1];
            }
            try
            {
                if (Client != null)
                {
                    Disconnect();
                }
                var factory = new MqttFactory();
                Client = factory.CreateMqttClient();
                var clientOptions = new MqttClientOptionsBuilder()
                       .WithClientId(uri.PathAndQuery)
                          .WithTcpServer(uri.Host, uri.Port)
                        .WithCredentials(username, password)
                        .Build();
                Client.ApplicationMessageReceivedAsync +=   Client_ApplicationMessageReceived;
                Client.ConnectedAsync+=  Client_ConnectedAsync;
                Client.DisconnectedAsync += async (MqttClientDisconnectedEventArgs e) =>
                {
                    try
                    {
                        await Client.ConnectAsync(clientOptions);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "CONNECTING FAILED");
                    }
                };

                try
                {
                    var result = await Client.ConnectAsync(clientOptions);
                    initok = result.ResultCode == MqttClientConnectResultCode.Success;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "CONNECTING FAILED");
                }
                _logger.LogInformation("WAITING FOR APPLICATION MESSAGES");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "CONNECTING FAILED");
            }
            return initok;
        }

 

        public void Disconnect()
        {
            try
            {
                Client.DisconnectAsync();
            }
            catch
            {
            }
            try
            {
                Client.Dispose();
            }
            catch
            {
            }
        }

        private async Task Client_ConnectedAsync(MqttClientConnectedEventArgs e)
        {

            await Client.SubscribeAsync($"devices/{DeviceId}/rpc/request/+/+");
            await Client.SubscribeAsync($"devices/{DeviceId}/attributes/update/", MqttQualityOfServiceLevel.ExactlyOnce);
            _logger.LogInformation($"CONNECTED WITH SERVER ");
            OnConnected?.Invoke(this, e);

        }
        /// <summary>
        /// 网关的字设备订阅名称， 不订阅 ID，因为 网关可能没法知道自己子设备的ID，但一定知道自己的ID，因此 连接时订阅自己的消息， 后期订阅子设备的消息
        /// </summary>
        /// <param name="name">子设备名称</param>
        public async Task SubscribeDeviceAsync(string name)
        {
            ///devices/SafetyBeltBox_863488055345618/rpc/request/clasp/6c86c79cbb2f486cb3ae5c491acb828d
            ///devices/SafetyBeltBox_863488055345618/rpc/request/+/+
            var res1 = await Client.SubscribeAsync($"devices/{name}/rpc/request/+/+", MqttQualityOfServiceLevel.ExactlyOnce);
            _logger.LogInformation($"订阅{name} {res1.ReasonString}");
            var resu2 = await Client.SubscribeAsync($"devices/{name}/attributes/update/", MqttQualityOfServiceLevel.ExactlyOnce);
            _logger.LogInformation($"订阅{name} {res1.ReasonString}");

        }
        private  Task Client_ApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            _logger.LogInformation($"ApplicationMessageReceived Topic {e.ApplicationMessage.Topic}  QualityOfServiceLevel:{e.ApplicationMessage.QualityOfServiceLevel} Retain:{e.ApplicationMessage.Retain} ");
            try
            {
                if (e.ApplicationMessage.Topic.StartsWith($"devices/") && e.ApplicationMessage.Topic.Contains("/response/"))
                {
                    ReceiveAttributes(e);
                }
                else if (e.ApplicationMessage.Topic.StartsWith($"devices/") && e.ApplicationMessage.Topic.Contains("/rpc/request/"))
                {
                    var tps = e.ApplicationMessage.Topic.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    var rpcmethodname = tps[4];
                    var rpcdevicename = tps[1];
                    var rpcrequestid = tps[5];
                    _logger.LogInformation($"rpcmethodname={rpcmethodname} ");
                    _logger.LogInformation($"rpcdevicename={rpcdevicename } ");
                    _logger.LogInformation($"rpcrequestid={rpcrequestid}   ");
                    if (!string.IsNullOrEmpty(rpcmethodname) && !string.IsNullOrEmpty(rpcdevicename) && !string.IsNullOrEmpty(rpcrequestid))
                    {
                        OnExcRpc?.Invoke(Client, new RpcRequest()
                        {
                            Method = rpcmethodname,
                            DeviceId = rpcdevicename,
                            RequestId = rpcrequestid,
                            Params = e.ApplicationMessage.ConvertPayloadToString()
                        });
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ClientId:{e.ClientId} Topic:{e.ApplicationMessage.Topic},Payload:{e.ApplicationMessage.ConvertPayloadToString()}");
            }
            return Task.CompletedTask;
        }

        private void ReceiveAttributes(MqttApplicationMessageReceivedEventArgs e)
        {
            var tps = e.ApplicationMessage.Topic.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var rpcmethodname = tps[2];
            var rpcdevicename = tps[1];
            var rpcrequestid = tps[4];
            _logger.LogInformation($"rpcmethodname={rpcmethodname} ");
            _logger.LogInformation($"rpcdevicename={rpcdevicename } ");
            _logger.LogInformation($"rpcrequestid={rpcrequestid}   ");

            if (!string.IsNullOrEmpty(rpcmethodname) && !string.IsNullOrEmpty(rpcdevicename) && !string.IsNullOrEmpty(rpcrequestid))
            {
                if (e.ApplicationMessage.Topic.Contains("/attributes/"))
                {
                    OnReceiveAttributes?.Invoke(Client, new AttributeResponse()
                    {
                        KeyName = rpcmethodname,
                        DeviceName = rpcdevicename,
                        Id = rpcrequestid,
                        Data = e.ApplicationMessage.ConvertPayloadToString()
                    });
                }
            }
        }

        public Task UploadAttributeAsync(object obj) => UploadAttributeAsync("me", obj);


        public Task UploadAttributeAsync(string _devicename, object obj)
        {
            return Client.PublishAsync($"devices/{_devicename}/attributes", Newtonsoft.Json.JsonConvert.SerializeObject(obj));
        }
        public Task UploadAttributeAsync(string _devicename,  Newtonsoft.Json.Linq.JObject jo)
        {
            return Client.PublishAsync($"devices/{_devicename}/attributes", jo.ToString());
        }
        public Task UploadTelemetryDataAsync(object obj) => UploadTelemetryDataAsync("me", obj);

        public Task UploadTelemetryDataAsync(string _devicename, object obj)
        {
            return Client.PublishAsync($"devices/{_devicename}/telemetry", Newtonsoft.Json.JsonConvert.SerializeObject(obj));
        }
        public Task UploadDeviceStatusAsync(string _devicename, bool online)
        {
            return Client.PublishAsync($"devices/{_devicename}/status/{(online ? "online" : "offline")}", Newtonsoft.Json.JsonConvert.SerializeObject(new { online }));
        }
        public Task ResponseExecommand(RpcResponse rpcResult)
        {
            ///IoTSharp/Clients/RpcClient.cs#L65     var responseTopic = $"/devices/{deviceid}/rpc/response/{methodName}/{rpcid}";
            string topic = $"devices/{rpcResult.DeviceId}/rpc/response/{rpcResult.Method.ToString()}/{rpcResult.ResponseId}";
            return Client.PublishAsync(  topic,   Newtonsoft.Json.JsonConvert.SerializeObject(rpcResult.Data) , MqttQualityOfServiceLevel.ExactlyOnce );
        }
        public Task RequestExecommand(RpcRequest rpcRequest)
        {
            ///IoTSharp/Clients/RpcClient.cs#L65     var responseTopic = $"/devices/{deviceid}/rpc/response/{methodName}/{rpcid}";
            string topic = $"devices/{rpcRequest.DeviceId}/rpc/request/{rpcRequest.Method.ToString()}/{rpcRequest.RequestId}";
            return Client.PublishAsync(topic, rpcRequest.Params.ToString(), MqttQualityOfServiceLevel.ExactlyOnce);
        }
        public Task RequestAttributes(params string[] args) => RequestAttributes("me", false, args);
        public Task RequestAttributes(string _device, params string[] args) => RequestAttributes(_device, false, args);
        public Task RequestAttributes(bool anySide = true, params string[] args) => RequestAttributes("me", true, args);

        public Task RequestAttributes(string _device, bool anySide, params string[] args)
        {
            string id = Guid.NewGuid().ToString();
            string topic = $"devices/{_device}/attributes/request/{id}";
            Dictionary<string, string> keys = new Dictionary<string, string>();
            keys.Add(anySide ? "anySide" : "server", string.Join(",", args));
            Client.SubscribeAsync($"devices/{_device}/attributes/response/{id}", MqttQualityOfServiceLevel.ExactlyOnce);
            return Client.PublishAsync(topic, Newtonsoft.Json.JsonConvert.SerializeObject(keys), MqttQualityOfServiceLevel.ExactlyOnce);
        }
    }

}

