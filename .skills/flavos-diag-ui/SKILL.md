---
name: flavos-diag-ui
description: Design e interface WPF do Flavos Diag - tokens de cor, tipografia, temas claro e escuro, ResourceDictionary, componentes, gráficos de sensores, animações com propósito e redução de movimento, acessibilidade e estados de carregamento, vazio, erro e "sem leitura". Use sempre que criar ou alterar Views XAML, temas, estilos, ícones, gráficos ao vivo, o botão de Diagnóstico rápido, o semáforo de vereditos ou qualquer coisa visual do app, e quando for aplicar a identidade do ecossistema Flavos ao aplicativo de Windows.
---

# Interface do Flavos Diag (WPF)

O app é usado na bancada, muitas vezes com a máquina do cliente aberta ao lado, com pressa e às vezes em ambiente pouco iluminado. O visual precisa ser limpo e legível, e comunicar estado sem ambiguidade. Evite o aspecto de "painel de ferramenta genérico": hierarquia clara, poucos elementos por tela, tipografia boa.

## Identidade (mesma do FlavosWare)
- Destaque: verde-petróleo `#0F766E`, com variação mais clara no tema escuro.
- Tema claro: fundo `#F4F7F5`, superfície branca, tinta `#0D1714`, linhas `#D8E3DE`.
- Tema escuro: fundo `#07110E`, superfície `#0E1A16`, tinta `#E7F0EB`, linhas `#22362E`.
- Semântica: verde = OK, âmbar = Atenção, vermelho = Falha, cinza = Inconclusivo / Não se aplica. **Nunca só cor**: sempre ícone + texto.
- Fonte: Geist Sans na interface e Geist Mono para números, códigos e identificadores, **se a licença permitir empacotar** (confirme em `docs/LICENCAS.md`); caso contrário, uma fonte livre equivalente. Valores numéricos com alinhamento tabular.
- Ícones de traço único e consistente (conjunto com licença verificada). Sem emojis, sem gradientes decorativos, sem sombras pesadas.

## Tokens e temas
- Cores, espaçamentos (múltiplos de 4), raios e tamanhos de fonte como `ResourceDictionary` de tokens (`Tokens.Light.xaml`, `Tokens.Dark.xaml`) e estilos em `Styles/*.xaml`. Nenhuma cor ou margem solta em Views.
- Troca de tema em tempo de execução com `DynamicResource`; respeitar o tema do Windows como padrão, com opção manual.
- Raios: 14 px em cartões, 10 px em campos, 8 px em chips.

## Estrutura de telas
Navegação lateral: Resumo, Sistema, Disco, Memória, CPU, GPU, Bateria, Rede, Checklist, Laudo.
- **Resumo**: botão **Diagnóstico rápido** em destaque; abaixo, o semáforo por categoria (ícone, nome, estado, uma linha de motivo). Clique abre a categoria.
- Cada categoria: veredito no topo, evidências (valor, limiar, unidade), limitações e recomendações, e só então os detalhes técnicos.
- Uma ação primária por tela; ações perigosas (estresse, varredura) exigem confirmação com resumo do que vai acontecer.

## Estados obrigatórios em toda tela
Carregando (esqueleto, não spinner infinito), vazio, erro (mensagem em pt-BR + o que fazer), **sem leitura** (explicar a causa: "Exige administrador", "Não exposto por este equipamento") e **não se aplica**. Nunca mostrar `0` ou `--` sem explicação.

## Gráficos de sensores
- Janela deslizante (60 s a 10 min), eixo com unidade, mínimo/máximo/média ao lado, linha do limite configurado em destaque.
- Série com cor + estilo/marcador distinguíveis (não depender só da cor). Alternativa em tabela para acessibilidade.
- Atualização em intervalo fixo (ex.: 1 s) com limite de pontos; sem recriar o gráfico a cada amostra.
- Durante estresse: a temperatura e o botão **Parar** ocupam o topo e nunca saem da tela.

## Animações (com propósito)
- Duração 150 a 250 ms, easing suave. Usos: transição entre telas (fade + leve deslize), entrada do resultado do semáforo, abertura de diálogos, destaque quando um valor muda de estado.
- Nada animado em loop contínuo sem função, e nada que atrapalhe a leitura de números ao vivo.
- Respeitar a preferência do Windows: se `SystemParameters.ClientAreaAnimation` for falso, desligar animações.
- Animar `Opacity` e `RenderTransform` (barato); evitar animar layout.

## Acessibilidade
- Contraste AA nos dois temas; foco visível; ordem de tabulação lógica; atalhos de teclado para as ações principais.
- `AutomationProperties.Name` em botões de ícone, gráficos e indicadores; estado do veredito disponível como texto para leitores de tela.
- Alvos de clique ≥ 40 px. Texto escalável com a configuração do Windows.

## Textos
Em pt-BR, arquivos `.resx`. Tom direto e sem jargão na camada de resumo; termos técnicos nos detalhes. Os nomes dos recursos e os textos são próprios (ver clean room: nada de nomes, strings ou layout de outros programas).

## Checklist por tela
- [ ] Usa só tokens e estilos compartilhados.
- [ ] Funciona nos temas claro e escuro; contraste conferido.
- [ ] Estados carregando, vazio, erro, sem leitura e não se aplica tratados.
- [ ] Informação de estado = ícone + texto, não só cor.
- [ ] Teclado e leitor de tela utilizáveis.
- [ ] Animações respeitam a redução de movimento.
- [ ] Captura de tela nos dois temas salva em `docs/capturas/`.
