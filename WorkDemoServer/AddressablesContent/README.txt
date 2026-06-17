把 Unity 构建出的 Addressables 远程内容放这里（充当 CDN）。

来源：Unity 工程构建后的 ServerData/ 目录（例如 ServerData/StandaloneWindows64/）。
做法：把 ServerData/* 整个拷到本目录下，保持子目录结构：

  AddressablesContent/StandaloneWindows64/catalog_xxx.json
  AddressablesContent/StandaloneWindows64/catalog_xxx.hash
  AddressablesContent/StandaloneWindows64/*.bundle

客户端访问地址：http://127.0.0.1:5188/addressables/StandaloneWindows64/...
对应 Addressables Profile 的 RemoteLoadPath = http://127.0.0.1:5188/addressables/[BuildTarget]
