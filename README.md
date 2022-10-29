# IoTSharp.Gateways 

IoTSharp的网关可以通过页面设置和添加采集映射信息可以完成自动化采集。主要功能如下:

1. 管理被采集的从机基本信息（已实现）
2. 管理采集点和IoTSharp的映射 （已实现）
3. 接收IoTSharp的下行数据和控制 (未实现)


IoTSharp物联网网关支持的协议:
 1. Modbus DTU协议: 使用 dtu开头，在Linux的串口路径使用 "." 替换"/"  波特率使用 BaudRate 为设置波特率 另外， 支持 Parity Handshake  StopBits 和 DataBits, 除了波特率， 其他参数均使用对应的枚举名称，  例如:   dtu://dev.ttyS0/?BaudRate=115200 
    在Widnows中 DTU格式为   dtu://COM1:115200
 2. Modbus RTU协议:  tcp://www.host.com:502
 3. Modbus 串口通过TCP透传:  d2t://www.host.com:502 
 4. OPC-UA 协议:     opc.tcp://127.0.0.1:49320


| 公众号 |    [QQ群63631741](https://jq.qq.com/?_wv=1027&k=HJ7h3gbO)  |  微信群  |
| ------ | ---- | ---- |
| ![](https://github.com/IoTSharp/IoTSharp/raw/master/docs/static/img/qrcode.jpg) | ![](https://github.com/IoTSharp/IoTSharp/raw/master/docs/static/img/IoTSharpQQGruop.png) | ![企业微信群](https://github.com/IoTSharp/IoTSharp/raw/master/docs/static/img/qyqun.jpg) |