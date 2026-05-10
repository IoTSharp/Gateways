using System.Net;
using System.Net.Sockets;
using MQTTnet.Server;

namespace IoTSharp.Edge.BasicRuntime.Tests;

public sealed class MqttBuiltInFunctionTests
{
    [Fact]
    public async Task Runtime_can_connect_ping_publish_subscribe_receive_and_disconnect()
    {
        var port = GetFreePort();
        var factory = new MqttServerFactory();
        var server = factory.CreateMqttServer(new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointBoundIPAddress(IPAddress.Loopback)
            .WithDefaultEndpointPort(port)
            .Build());

        await server.StartAsync();

        try
        {
            var runtime = new BasicRuntime();
            var result = runtime.Execute($$"""
                client = MQTT_CONNECT("127.0.0.1", {{port}}, "basic-mqtt-tests")
                if client = 0 then
                  return "connect failed: " + MQTT_LAST_ERROR()
                endif

                if MQTT_PING(client) = 0 then
                  return "ping failed: " + MQTT_LAST_ERROR(client)
                endif

                if MQTT_SUBSCRIBE(client, "basic/runtime/tests", 1) = 0 then
                  return "subscribe failed: " + MQTT_LAST_ERROR(client)
                endif

                if MQTT_PUBLISH(client, "basic/runtime/tests", "hello mqtt", 1, 0) = 0 then
                  return "publish failed: " + MQTT_LAST_ERROR(client)
                endif

                msg = MQTT_RECEIVE(client, 5000)
                if msg = nil then
                  return "no message"
                endif

                if msg("topic") <> "basic/runtime/tests" then
                  return "wrong topic: " + msg("topic")
                endif

                if msg("payload") <> "hello mqtt" then
                  return "wrong payload: " + msg("payload")
                endif

                if MQTT_UNSUBSCRIBE(client, "basic/runtime/tests") = 0 then
                  return "unsubscribe failed: " + MQTT_LAST_ERROR(client)
                endif

                if MQTT_DISCONNECT(client) = 0 then
                  return "disconnect failed: " + MQTT_LAST_ERROR(client)
                endif

                return "ok"
                """);

            Assert.Equal("ok", result.ReturnValue);
        }
        finally
        {
            await server.StopAsync(new MqttServerStopOptionsBuilder().Build());
        }
    }

    [Fact]
    public void Runtime_exposes_last_mqtt_error()
    {
        var runtime = new BasicRuntime();
        var result = runtime.Execute("""
            if MQTT_PING(999) = 0 then
              return MQTT_LAST_ERROR()
            endif
            return "unexpected"
            """);

        Assert.Equal("MQTT handle not found.", result.ReturnValue);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
