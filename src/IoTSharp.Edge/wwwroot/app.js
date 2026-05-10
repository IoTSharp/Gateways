const statusBar = document.getElementById('statusBar')
const configMeta = document.getElementById('configMeta')
const diagnosticsMeta = document.getElementById('diagnosticsMeta')
const configEditor = document.getElementById('configEditor')
const diagnosticsOutput = document.getElementById('diagnosticsOutput')
const reloadConfigButton = document.getElementById('reloadConfigButton')
const saveConfigButton = document.getElementById('saveConfigButton')
const refreshDiagnosticsButton = document.getElementById('refreshDiagnosticsButton')

function setStatus(message, tone = 'info') {
  statusBar.textContent = message
  if (tone === 'ok') {
    statusBar.style.background = '#edf8ef'
    statusBar.style.borderColor = 'rgba(29, 107, 57, 0.25)'
    statusBar.style.color = '#1d6b39'
    return
  }

  if (tone === 'warn') {
    statusBar.style.background = '#fff4e5'
    statusBar.style.borderColor = 'rgba(158, 93, 0, 0.24)'
    statusBar.style.color = '#9e5d00'
    return
  }

  statusBar.style.background = '#eef5f4'
  statusBar.style.borderColor = 'rgba(22, 99, 107, 0.18)'
  statusBar.style.color = '#0f4b51'
}

function formatDate(value) {
  if (!value) {
    return '未生成'
  }

  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

async function request(path, init) {
  const response = await fetch(path, {
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
    ...init,
  })

  if (!response.ok) {
    const text = await response.text()
    throw new Error(text || `${response.status} ${response.statusText}`)
  }

  return response.json()
}

async function loadConfig() {
  const config = await request('/api/bootstrap/config')
  configEditor.value = config.json ?? ''
  configMeta.textContent = [
    `文件: ${config.filePath}`,
    `存在: ${config.exists ? '是' : '否，当前显示模板'}`,
    `最后更新时间: ${formatDate(config.lastWriteTimeUtc)}`,
  ].join(' | ')
}

async function loadDiagnostics() {
  const diagnostics = await request('/api/diagnostics/summary')
  diagnosticsOutput.textContent = JSON.stringify(diagnostics, null, 2)

  diagnosticsMeta.textContent = [
    `生成时间: ${formatDate(diagnostics.generatedAtUtc)}`,
    `进程: ${diagnostics.process.name} (${diagnostics.process.id})`,
    `Edge 上报: ${diagnostics.edgeReporting.enabled ? '已启用' : '未启用'}`,
    `Token: ${diagnostics.edgeReporting.hasAccessToken ? '已配置' : '未配置'}`,
  ].join(' | ')
}

async function reloadAll() {
  setStatus('正在刷新 bootstrap 配置和诊断信息...')
  await Promise.all([loadConfig(), loadDiagnostics()])
  setStatus('页面数据已刷新。', 'ok')
}

async function saveConfig() {
  saveConfigButton.disabled = true
  setStatus('正在保存 bootstrap.json ...')

  try {
    const saved = await request('/api/bootstrap/config', {
      method: 'POST',
      body: JSON.stringify({
        json: configEditor.value,
      }),
    })

    configEditor.value = saved.json ?? configEditor.value
    await loadConfig()
    await loadDiagnostics()
    setStatus('bootstrap.json 已保存，运行时会按最新配置继续工作。', 'ok')
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error)
    setStatus(`保存失败: ${message}`, 'warn')
  } finally {
    saveConfigButton.disabled = false
  }
}

reloadConfigButton.addEventListener('click', async () => {
  reloadConfigButton.disabled = true
  try {
    await loadConfig()
    setStatus('已重新加载 bootstrap 配置。', 'ok')
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error)
    setStatus(`读取配置失败: ${message}`, 'warn')
  } finally {
    reloadConfigButton.disabled = false
  }
})

refreshDiagnosticsButton.addEventListener('click', async () => {
  refreshDiagnosticsButton.disabled = true
  try {
    await loadDiagnostics()
    setStatus('诊断信息已刷新。', 'ok')
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error)
    setStatus(`刷新诊断失败: ${message}`, 'warn')
  } finally {
    refreshDiagnosticsButton.disabled = false
  }
})

saveConfigButton.addEventListener('click', saveConfig)

reloadAll().catch((error) => {
  const message = error instanceof Error ? error.message : String(error)
  setStatus(`初始化失败: ${message}`, 'warn')
})
