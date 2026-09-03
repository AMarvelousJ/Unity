# SL15 Unity 演示场景

- 场景：`Assets/Shiploader/Scenes/SL15_Demo.unity`
- 重建：Unity 菜单 `Tools/SL15/Rebuild Demo Scene`
- 比例：`1 Unity Unit = 1 m`
- 坐标：网页 `(x, y, z)` 映射为 Unity `(x, z, y)`

进入 Play Mode 后，左侧中文面板可控制大车行走、臂架回转、臂架俯仰、臂架伸缩和溜筒回转，并可切换相机、皮带、煤流和舱盖。透视相机支持鼠标右键拖动与滚轮缩放。

右侧“一键装船”面板连接 `http://127.0.0.1:8000`，与网页共享 FastAPI 的三轮十二批任务、五轴姿态、皮带/煤流、五舱吨位和排水倒计时。请先在网站工程目录手动执行：

```bash
python -m uvicorn app.main:app --reload
```

Unity 会自动注册服务端仿真时钟并断线重试；服务不可用时保留离线手动演示。活动任务期间手动五轴、皮带和舱盖控制会锁定。

`Generated` 目录由场景生成器维护，可安全重复生成；请勿在该目录内手工保存需要长期保留的改动。原始 `Assets/Scenes/SampleScene.unity` 不参与生成流程。

本场景仅用于编辑器仿真演示。FastAPI 同步只连接本机仿真服务，不接管真实 PLC，不用于真实设备控制、稳性决策或制造级尺寸校核。
