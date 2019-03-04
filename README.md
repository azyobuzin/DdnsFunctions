# DdnsFunctions for ConoHa
ConoHa の DNS レコードを更新する Azure Functions 関数です。

## 使い方
ConoHa コントロールパネルの API タブから、次の設定を取得し、 Azure Functions のアプリケーション設定に設定します。

| アプリケーション設定 | ConoHa コントロールパネル内の名称 |
| ------------------ | ------------------------------- |
| DDNSFUNCTIONS_CONOHA_IDENTITY_SERVICE_ENDPOINT | 「エンドポイント」の「Identity Service」 |
| DDNSFUNCTIONS_CONOHA_USERNAME | 「APIユーザー」の「ユーザー名」 |
| DDNSFUNCTIONS_CONOHA_PASSWORD | 「APIユーザー」の「パスワード」 |
| DDNSFUNCTIONS_CONOHA_TENANTID | 「テナント情報」の「テナントID」 |

Azure Functions にデプロイしたら、次の URI に POST リクエストすることで、 DNS レコードが更新されます。

```
https://{AppName}.azurewebsites.net/api/UpdateRecord_HttpStart?code={FunctionKey}&domain=your.domain.&record=www&value=192.0.2.1
```

`value` パラメータを省略すると、リクエスト元の IP アドレスが使用されます。 Azure Functions アプリへの接続は IPv4 なはずなので、 AAAA レコードの更新をする場合は `value` を指定してください。
