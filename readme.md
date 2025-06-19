# BlazorVoice

## Docker로 빌드 및 실행하기

```bash
docker build -t blazorvoice .
```

```bash
docker run -d -p 8080:8080 --name blazorvoice blazorvoice
```


```bash
docker build -t registry.webnori.com/blazorvoice:dev .
```

```bash
docker push registry.webnori.com/blazorvoice:dev
```

