# WslContainerd.Ollama

Ollama container lifecycle and API helpers for Windows via WSL2 + containerd.

Repository: https://github.com/XiaEmrys/WslContainerd.Ollama

## Dependencies

Requires **WslContainerd.Net** abstractions (`WslContainerd.Logging.Abstractions`, `WslContainerd.Services.Abstractions`, `WslContainerd.Net.Abstractions`).

- In the Evolux monorepo: resolved via `ProjectReference` to `../WslContainerd.Net/`.
- In this standalone repo: resolved via NuGet `PackageReference` (v0.1.0). Publish or install [WslContainerd.Net](https://github.com/XiaEmrys/WslContainerd.Net) packages first.

## Options

| Property | Default | Description |
|----------|---------|-------------|
| `ImageName` | `ollama/ollama:latest` | Container image |
| `ContainerName` | `ollama` | Container name |
| `BaseUrl` | `http://localhost:11434` | Ollama HTTP API |
| `HostPort` | `11434` | Host port mapping |

## Build

```bash
dotnet build WslContainerd.Ollama.sln -c Release
dotnet pack WslContainerd.Ollama.sln -c Release -o artifacts/nuget
```

## Sync with Evolux (subtree)

See [docs/wsl-containerd-subtree.md](../../docs/wsl-containerd-subtree.md) in the Evolux repo.

```powershell
.\scripts\wsl-containerd-ollama-subtree.ps1 -Split
.\scripts\wsl-containerd-ollama-subtree.ps1 -Push
```
