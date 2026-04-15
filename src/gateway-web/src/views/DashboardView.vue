<template>
  <section class="hero panel">
    <div>
      <span class="badge">DDD + API + Worker + Vue 3</span>
      <h1>IoTSharp Gateways</h1>
      <p>
        当前前端壳面向统一驱动目录、通道/设备/点位配置、采集任务、转换规则、上传通道和调试读写。
      </p>
    </div>
  </section>

  <section class="card-grid">
    <article class="panel card">
      <h2>统一驱动</h2>
      <p>后端通过统一读写接口封装 Modbus、Siemens、Mitsubishi、Omron FINS、Allen-Bradley。</p>
    </article>
    <article class="panel card">
      <h2>采集编排</h2>
      <p>Worker 使用统一任务模型执行轮询、转换与上传流程。</p>
    </article>
    <article class="panel card">
      <h2>前后端分离</h2>
      <p>Vue 3 前端通过 REST API 管理配置，并提供调试读写入口。</p>
    </article>
  </section>
</template>
