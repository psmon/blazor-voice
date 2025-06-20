# BlazorVoice

## Docker로 빌드 및 실행하기

```bash
docker build -t blazorvoice .
```

```bash
docker run -d -p 8080:8080 --name blazorvoice blazorvoice
```


## Private Build

```bash
docker build -t registry.webnori.com/blazorvoice:dev .
```

```bash
docker push registry.webnori.com/blazorvoice:dev
```

## Docker-Compose

```
version: '2'
services:
  blazor-voice-app:
    image: registry.webnori.com/blazorvoice:dev
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      OPENAI_API_KEY: .....
    ports:
    - 8002:8080/tcp
    labels:
      io.rancher.scheduler.affinity:host_label: server=late02
      io.rancher.container.hostname_override: container_name
      io.rancher.container.pull_image: always
```

