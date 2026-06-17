print("[Lua] time_feature.lua loaded (HOT-UPDATED 新模块)")

function ShouldShowTimeButton()
    return true
end

function OnSettingTimeButton()
    print("[Lua] 打开 SetTimePanel（来自热更新模块）")
    CS.LuaApi.OpenSetTimePanel()
end

function OnTimeButton(which)
    if which == 'noon' then
        CS.LuaApi.SetTime(12)            -- 12Button -> 正午
        print("[Lua] -> 时间设为 正午(12:00)")
    elseif which == 'night' then
        CS.LuaApi.SetTime(24)            -- 24Button -> 午夜
        print("[Lua] -> 时间设为 午夜(24:00)")
    end
end
