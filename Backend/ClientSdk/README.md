# HAREKÂT Client SDK

Bağımlılıksız `HttpClient` istemcisi — Unity'ye kopyalanmaya hazır.

## Unity'ye aktarma

1. `HarekatClient.cs` dosyasını `Assets/_Project/Scripts/ClientSdk/` altına kopyala.
2. UnityWebRequest kullanma; `System.Net.Http.HttpClient` yeterlidir (Unity 2022+).
3. IL2CPP için `link.xml` ile `System.Net.Http` korunmalı.

```csharp
using var api = new Harekat.ClientSdk.HarekatClient("https://api.example.com");
await api.LoginAsync("Asker42", "password123");
var me = await api.GetMeAsync();
```
