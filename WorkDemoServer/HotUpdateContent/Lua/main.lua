-- main.lua —— Lua 逻辑入口（热更前·基线版本 version 1）
-- 此刻没有"改时间"功能：设置面板里看不到 TimeButton。
-- 演示时：新建 time_feature.lua，并在本文件末尾加一行 require('time_feature')，
-- 不重新打包客户端、只发布 + 重开，时间功能就被"插"进来了。

print("[Lua] main.lua loaded -> version 1 (baseline, 无时间功能)")

-- 设置面板是否显示 TimeButton：基线返回 false（不显示）
function ShouldShowTimeButton()
    return false
end

require('time_feature')
