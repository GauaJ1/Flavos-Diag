---
name: csharp-wpf-standards
description: Padrões de C# e WPF (.NET 8) para o Flavos Diag: configuração do projeto e analisadores, MVVM com CommunityToolkit, injeção de dependência, async e cancelamento, threads e interface, descarte de recursos, tratamento de erros, logging, globalização pt-BR, publicação self-contained e estrutura de solução em camadas. Use sempre que criar ou editar arquivos .cs, .xaml, .csproj, Directory.Build.props ou testes do Flavos Diag, e ao decidir como organizar projetos, ViewModels, serviços ou comandos assíncronos.
---

# C# e WPF no Flavos Diag

Um app de diagnóstico faz muita coisa lenta (WMI, processos, leitura de disco) enquanto mostra gráficos em tempo real. Os erros típicos são interface congelada, vazamento de recursos e exceções engolidas que viram "tudo OK". Estes padrões existem para evitar isso.

## Estrutura da solução
```
src/
  FlavosDiag.Core/        modelos, veredito, pontuação (sem dependência de Windows)
  FlavosDiag.Providers/   coletores atrás de interfaces + versões Fake
  FlavosDiag.Workloads/   cargas de CPU/GPU (estresse e benchmark)
  FlavosDiag.Reports/     PDF e JSON
  FlavosDiag.App/         WPF (Views, ViewModels, temas)
tests/FlavosDiag.Tests/   xUnit
```
Dependências só apontam para dentro: App → Reports/Providers/Workloads → Core. Core nunca referencia Windows, WPF nem bibliotecas de sensores. Isso permite testar veredito e pontuação em qualquer máquina.

## Configuração do projeto (Directory.Build.props)
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>   <!-- App: net8.0-windows -->
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>
```
Adicione um `.editorconfig` na raiz. Warning novo é erro: corrija a causa, não suprima, salvo com comentário explicando.

## Regras de código
- **Modelos imutáveis**: `record` com `init`. Resultados de coleta usam um tipo explícito (`ProviderResult<T>`: `Ok`, `Unavailable`, `NeedsAdmin`, `Error` + motivo), nunca `null` ou valor padrão para "não sei".
- **Async de ponta a ponta**: nada de `.Result`, `.Wait()` ou `GetAwaiter().GetResult()` na interface. Toda operação longa recebe `CancellationToken` e o respeita. Código de biblioteca (Core, Providers, Workloads) usa `ConfigureAwait(false)`; ViewModels não.
- **Trabalho pesado fora da thread da interface**: coleta em `Task.Run` ou serviços em segundo plano; cargas de estresse em threads dedicadas. A interface só recebe resultados. Atualizações de UI vindas de outra thread passam por `Dispatcher` ou `IProgress<T>`.
- **Descarte**: tudo que é `IDisposable` (objetos de sensores, `Process`, `ManagementObjectSearcher`, `ManagementObject`, streams) em `using`. O serviço de sensores é singleton e descartado ao fechar o app.
- **Erros**: capture exceções específicas na borda do provider e converta em `ProviderResult` com causa legível em pt-BR. Nunca `catch { }` vazio. Exceção inesperada em tarefa de fundo é registrada em log e vira "Inconclusivo", não derruba o app.
- **Tempo**: `DateTimeOffset` em UTC internamente; converter para hora local só na exibição. Medições de duração com `Stopwatch`.
- **Globalização**: interface com `CultureInfo("pt-BR")`; JSON e CSV com `InvariantCulture` e ISO 8601. Números nunca formatados com cultura implícita ao serializar.
- **Nomes**: código em inglês, textos de interface em pt-BR em arquivos de recursos (`.resx`), sem strings soltas no XAML ou no C#.

## MVVM
- `CommunityToolkit.Mvvm`: `[ObservableProperty]`, `[RelayCommand]` (com `AsyncRelayCommand` e `CanExecute`), `ObservableObject`. Sem lógica no code-behind, exceto o que é puramente visual.
- Injeção de dependência com `Microsoft.Extensions.Hosting` / `DependencyInjection`. Providers e serviços registrados por interface; ViewModels recebem interfaces no construtor.
- Navegação por um serviço de navegação, não por instanciar janelas dentro de ViewModels.
- Comandos longos expõem estado (`IsRunning`, progresso) e permitem cancelar. Botão de parada de estresse chama o cancelamento diretamente e nunca depende de a coleta terminar.
- Listas grandes (amostras de sensores) com janela deslizante de tamanho fixo; não deixe a coleção crescer sem limite.

## Teste e verificação (comandos)
```
dotnet restore --locked-mode
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
```
Cole o resultado resumido em `PROGRESS.md` ao fechar cada fase.

## Publicação
```
dotnet publish src/FlavosDiag.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
Confirme em máquina real que bibliotecas nativas e o binário externo (ex.: smartctl) ficam acessíveis ao app publicado. Pasta final inclui `licenses/` com os textos exigidos.

## O que evitar
- Chamar WMI/powercfg/processos na thread da interface.
- `static` com estado mutável (testes ficam não determinísticos).
- Valores padrão que parecem leitura real (`0 °C`, `100 %`). Sem leitura = ausência explícita.
- Código específico de Windows dentro do Core.
