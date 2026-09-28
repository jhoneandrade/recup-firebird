# 🛠️ Recuperador de Banco de Dados Firebird (RecupBD)

<p align="center">
  <img src="app_icon.ico" width="80" height="80" alt="RecupBD Icon" />
</p>

<p align="center">
  <b>Solução desktop moderna, não destrutiva e com interface Dark Theme para diagnóstico, reparo físico de páginas corrompidas e reconstrução lógica de bancos de dados relacionais Firebird (.IB / .FDB / .GDB).</b>
</p>

<p align="center">
  <a href="https://microsoft.com/windows"><img src="https://img.shields.io/badge/Platform-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Platform" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET_Framework-4.0%2B-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET" /></a>
  <a href="https://firebirdsql.org/"><img src="https://img.shields.io/badge/Firebird-2.0%20%7C%202.1%20%7C%202.5%20%7C%203.0-FF4500?style=for-the-badge&logo=firebird&logoColor=white" alt="Firebird" /></a>
  <img src="https://img.shields.io/badge/Vers%C3%A3o-1.2.0-16A34A?style=for-the-badge" alt="Version" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/Licen%C3%A7a-MIT-blue.svg?style=for-the-badge" alt="License" /></a>
</p>

---

## 📸 Demonstração Geral do Processo

Abaixo, a visualização completa do fluxo: a confirmação prévia com resumo de ações, o log em tempo real com barra de progresso em 100% e o resumo cronológico das 7 etapas concluídas com sucesso.

<p align="center">
  <img src="docs/workflow_overview.png" alt="Visão Geral do Fluxo de Recuperação" width="950" />
</p>

---

## 🎯 Por que o RecupBD foi criado?

A rotina tradicional de recuperação de bancos Firebird corrompidos normalmente exige:
1. Abrir prompts de comando (`cmd`) manuais como Administrador.
2. Descobrir e parar serviços do Firebird no painel `services.msc`.
3. Exportar variáveis de ambiente sensíveis (`SET ISC_USER=...` e `SET ISC_PASSWORD=...`) expondo senhas em scripts ou histórico do shell.
4. Digitar comandos extensos e suscetíveis a erro com `gfix` e `gbak`.
5. Risco constante de sobrescrever ou danificar a base original caso um comando falhe no meio.

O **RecupBD** transforma todo esse procedimento arriscado em uma **operação automatizada de 1 clique**, com total isolamento do banco original e monitoramento visual em tempo real.

---

## ✨ Recursos e Galeria da Aplicação

### 1. 🖥️ Tela Principal Limpa e Segura
Inicia totalmente em branco na primeira execução. Permite arrastar o arquivo de banco para a janela ou selecionar via diálogo. Conta com botão de revelação de senha (olho vetorial), verificação dinâmica de espaço livre em disco e opção de persistência criptografada.

<p align="center">
  <img src="docs/main_screen.png" alt="Tela Inicial do RecupBD" width="850" />
</p>

---

### 2. 🛡️ Diálogo de Confirmação e Segurança Preventiva
Antes de tocar em qualquer serviço ou arquivo, o sistema valida se a unidade possui espaço suficiente (garantindo no mínimo o dobro do tamanho do banco) e abre uma caixa de diálogo nativa escura resumindo exatamente o que será feito.

<p align="center">
  <img src="docs/confirm_dialog.png" alt="Caixa de Diálogo de Confirmação" width="750" />
</p>

---

### 3. 📊 Terminal com Abas e Filtro Inteligente de Erros
Esqueça telas pretas indecifráveis. O terminal moderno possui três modos de visualização:
- **`Todos os Logs`**: Detalhamento técnico completo de todas as saídas do `gfix` e `gbak`.
- **`! Somente Erros (X)`**: Filtro dedicado que isola corrupções reais de páginas (`bad checksum`, `wrong page type`, etc.), descartando milhares de linhas normais de metadados.
- **`Etapas do Processo`**: Visão executiva das etapas concluídas com data e hora.

<p align="center">
  <img src="docs/execution_logs.png" alt="Terminal de Logs em Tempo Real" width="850" />
</p>

---

### 4. ⚙️ Resumo Ordenado das Etapas Concluídas
Aba focada em auditoria rápida, documentando o horário exato da parada de serviços, salvamento do backup com data/hora, exportação lógica e substituição final bem-sucedida.

<p align="center">
  <img src="docs/process_steps.png" alt="Aba Etapas do Processo" width="850" />
</p>

---

### 5. 👨‍💻 Janela "Sobre o Desenvolvedor"
Acessível pelo botão `(!)` no cabeçalho superior direito. Apresenta informações da versão, build e botões diretos para perfis profissionais no LinkedIn e GitHub.

<p align="center">
  <img src="docs/about_developer.png" alt="Janela Sobre o Desenvolvedor" width="550" />
</p>

---

## 🔄 Fluxo de Recuperação Não Destrutivo

```mermaid
flowchart TD
    Start([Início]) --> CheckDisk[1. Verificação de Espaço em Disco]
    CheckDisk --> StopSvc[2. Parada Segura dos Serviços Firebird]
    StopSvc --> BkpOrig[3. Preservação do Banco Original com Data/Hora na Origem]
    BkpOrig --> StartSvc[4. Reinicialização do Serviço Firebird]
    StartSvc --> GfixMend[5. Varredura e Correção de Páginas: gfix -v -full & gfix -mend]
    GfixMend --> GbakBkp[6. Extração de Dados Estruturados: gbak -b -v -ig -g -l]
    GbakBkp --> GbakRest[7. Reconstrução Limpa e Novos Índices: gbak -c & gfix -v -f]
    GbakRest --> ReplaceOk[Conclusão: Substituição do Banco Operacional no Destino]
    ReplaceOk --> End([Banco 100% Operacional e Validado])
```

---

## 🔐 Segurança e Privacidade

- **Criptografia Local de Credenciais**: Quando a opção *"Salvar dados neste computador"* está ativada, a senha é armazenada localmente com **AES-128-CBC** (`recupbd.cfg`). Nenhuma credencial trafega em texto puro.
- **Isolamento Total**: O banco original do cliente nunca sofre reparos diretos no mesmo arquivo. Ele é preservado com timestamp e todo o reparo acontece sobre uma cópia de trabalho em sandbox.
- **Zero Vazamentos no Repositório**: Arquivos de dados (`.IB`, `.FDB`, `.FBK`, `.GDB`), arquivos de configuração local (`.cfg`) e scripts legados estão estritamente bloqueados pelo `.gitignore`.

---

## 🚀 Como Executar

1. Baixe o executável [`RecupBD.exe`](RecupBD.exe) diretamente deste repositório (ou compile pelo código-fonte).
2. Execute como **Administrador** (necessário para gerenciar os serviços do Firebird no Windows).
3. Selecione o arquivo de banco de dados (`.IB` ou `.FDB`).
4. Informe o Usuário e a Senha (ou marque para salvar no computador).
5. Clique em **▶ Iniciar Recuperação**.

---

## 🔨 Como Compilar o Código Fonte

O projeto foi construído em **C#** puro sobre o **.NET Framework 4.0** nativo do Windows, sem necessidade de ferramentas pesadas de terceiros.

### Opção 1: Via Script `build.bat` (1 Clique)
Basta dar um duplo clique no arquivo [`build.bat`](build.bat) na raiz do projeto.

### Opção 2: Linha de Comando Direta (`csc.exe`)
Execute no Prompt de Comando do Windows:
```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /codepage:65001 /target:winexe /win32icon:app_icon.ico /win32manifest:app.manifest /r:System.ServiceProcess.dll /out:RecupBD.exe RecupBD.cs
```

### Opção 3: Script Python com Ajuste de Idioma PE
```cmd
python build.py
```

---

## 💻 Requisitos do Sistema

- **Windows**: Windows 7 SP1, 8, 8.1, 10, 11 ou Windows Server 2008 R2+.
- **Firebird**: Versões 2.0, 2.1, 2.5, 3.0 ou 4.0.
- **Utilitários**: O programa busca `gbak.exe` e `gfix.exe` no Registro do Windows, nas pastas de instalação do Firebird ou na mesma pasta do executável.

---

## 📄 Licença

Este projeto está licenciado sob os termos da licença [MIT](LICENSE).

---

<p align="center">
  <b>Desenvolvido por Jhone Andrade</b><br/>
  <i>Uberlândia - MG • Analista de Sistemas</i><br/>
  <a href="https://br.linkedin.com/in/jhone-andrade-b450ab164">LinkedIn</a> • 
  <a href="https://github.com/jhoneandrade">GitHub</a>
</p>
