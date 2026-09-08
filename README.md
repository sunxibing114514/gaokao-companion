# 高考倒计时伴侣 (GaokaoCompanion)

M3(Material Design 3)风格 Windows 桌面工具:高考倒计时桌面组件 + 全局快捷键句子语音/网易云音乐搜索 + 定时壁纸切换。全部本地配置保存在 **exe 同目录下的一个 `config.json`** 中。

![tech](https://img.shields.io/badge/.NET-8.0_WPF-blue) ![build](https://img.shields.io/badge/build-GitHub_Actions-green)

## 功能

### 1. 高考倒计时桌面组件
- 从外部链接拉取 JSON 并解析:`date`、`sentences`、`event`
- 显示格式:`距离{event}还有X年X月X天X时X分X秒丨句子`
- 句子自动轮换,间隔可在设置中自定义(分钟)
- 桌面显示、可拖拽、**长按(0.7 秒)弹出菜单可选择关闭**
- 字体大小、组件背景不透明度可在设置中修改
- 自动换行:换行内容出现在 **“丨”之后**(句子区域独立换行)
- 双击组件 = 立即切换下一句;数据加载失败 60 秒自动重试

### 2. 句子语音搜索(全局快捷键 `Ctrl+H+J+S`)
- 弹出**上部居中**的搜索框,透明度可配置(**默认 30**)
- 句子来自外部 JSON(格式:`{ "句子": "声音链接", ... }`)
- 输入筛选 → ↑↓ 选择 → 回车/双击播放对应声音(**不循环**)
- `Ctrl+H+J+C` 关闭正在播放的声音

### 3. 网易云音乐搜索(全局快捷键 `Ctrl+H+J+N`)
- 弹出上部居中搜索框(透明度同上,默认 30)
- 同时搜索**歌名**(type=1)与**歌词**(type=1006),结果合并显示(歌词命中带「词」标记)
- ↑↓ 选择搜索结果,回车播放
- **歌词命中的歌曲:播放时直接跳到歌词对应位置**
- API 地址在设置中配置(NeteaseCloudMusicApi / Enhanced 兼容),**不硬编码**
- 可选配置**网易云 Cookie**(`MUSIC_U=…;`,设置界面或 config.json 的 `neteaseCookie`),可解锁 VIP 歌曲;不填则匿名访问
- `Ctrl+H+J+C` 停止播放;`Esc` 关闭搜索框;失焦自动关闭

### 4. 定时壁纸切换
- 规则:**星期 x 点 x 分 → 切换为指定壁纸(本地路径)**
- 规则保存在 config.json,可在设置 GUI 中增删改、浏览选择图片
- 可设置多条规则;`星期=0` 表示每天

## 快捷键

| 快捷键 | 作用 |
|---|---|
| `Ctrl+H+J+S` | 句子语音搜索框 |
| `Ctrl+H+J+N` | 网易云音乐搜索框 |
| `Ctrl+H+J+C` | 停止当前播放(语音/音乐) |

> 组合键需同时按住;由低级键盘钩子全局监听。

## 配置文件(config.json)

首次运行自动生成在 **exe 同目录**,示例见 [`config.example.json`](config.example.json):

```jsonc
{
  "countdownJsonUrl": "https://…/countdown.json",   // 倒计时数据源
  "soundsJsonUrl": "https://…/sounds.json",         // 句子→声音映射
  "neteaseApiBase": "https://wyyapi.hjymoon.us.ci/",// 网易云 API(可换任意兼容实例)
  "neteaseCookie": "",                              // 网易云 Cookie(如 MUSIC_U=xxx;),可解锁 VIP 歌曲,留空匿名
  "sentenceIntervalMinutes": 5,                     // 句子切换间隔(分钟)
  "fontSize": 30,                                   // 组件字体大小
  "sentenceMaxWidth": 560,                          // 句子换行区最大宽度
  "countdownMaxWidth": 720,
  "widgetBackgroundOpacity": 0,                     // 0=纯文字透明背景,100=不透明卡片
  "widgetTopmost": true,
  "widgetLeft": null, "widgetTop": null,            // 组件位置(自动保存)
  "searchOpacity": 30,                              // 搜索框不透明度(默认 30)
  "volume": 80,
  "wallpapers": [                                   // 定时壁纸规则
    { "day": 1, "hour": 8, "minute": 0, "imagePath": "C:\\wallpapers\\monday.jpg" }
  ]
}
```

外部数据源格式:

```jsonc
// countdownJsonUrl 指向的 JSON
{
  "date": "2026-06-07 09:00:00",
  "event": "2026年高考",
  "sentences": ["愿你合上笔盖的刹那,有侠客收剑入鞘的骄傲", "…"]
}

// soundsJsonUrl 指向的 JSON
{
  "句子一": "https://…/a.mp3",
  "句子二": "https://…/b.mp3"
}
```

## 下载使用

到 [Releases](../../releases) 下载 `GaokaoCompanion.exe`(自包含单文件,无需安装 .NET),双击运行:

1. 首次运行在 exe 同目录生成 `config.json`
2. 托盘图标 → 设置…,填写数据源地址、API 地址、壁纸规则
3. 保存并应用即可

> 说明:程序需要联网访问你配置的 JSON/API;配置与日志(`log.txt`)均保存在 exe 同目录。

## 本地开发

```bash
# Windows 上
dotnet run --project src/GaokaoCompanion

# 发布单文件 exe
dotnet publish src/GaokaoCompanion/GaokaoCompanion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

## 构建(CI)

推送到 `main` 自动触发 GitHub Actions 构建 exe(Actions 产物),推送 `v*` 标签会额外创建 Release 并附带 exe。

## 许可

MIT
