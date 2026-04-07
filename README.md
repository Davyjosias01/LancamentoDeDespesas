# Relatório técnico detalhado do projeto UIPath – sequência de ações e insumos de fluxo

## 1. Objetivo deste relatório

Este documento foi montado para servir como base de documentação técnica e também como insumo para uma IA gerar um fluxograma do projeto.

O foco aqui é:
- descrever a **arquitetura geral** do robô;
- registrar a **sequência de execução** do `Main.xaml`;
- detalhar a lógica dos **XAMLs secundários e terciários**;
- explicar como o **`Config.xlsx` alimenta o dictionary `Config`**;
- destacar **decisões, exceções, retries, integrações e pontos de atenção**.

---

## 2. Arquivos analisados

### XAMLs
- `Main.xaml`
- `InitAllSettings.xaml`
- `InitAllApplications.xaml`
- `CloseAllApplications.xaml`
- `KillAllProcesses.xaml`
- `GetTransactionData.xaml`
- `Process.xaml`
- `RetryCurrentTransaction.xaml`
- `SetObligationStatus.xaml`
- `TakeScreenshot.xaml`
- `AbreOFechamento.xaml`
- `AlteraPeriodoDeTrabalho.xaml`
- `DesejaGravarOsDadosNotification.xaml`
- `GenerateImportationLists.xaml`
- `GenerateImportationArchives.xaml`
- `GetFullCompany.xaml`
- `VerificacaodaImportacao.xaml`
- `IntegrateAllExpenses.xaml`
- `IntegrateExpense.xaml`
- `GetExpenses.xaml`

### Arquivo de configuração
- `Config.xlsx`

---

## 3. Visão geral do processo

O projeto está estruturado em cima de um **REFramework adaptado**, com `Main.xaml` em formato de **State Machine** e com estes estados principais:

1. **Initialization**
2. **Get Transaction Data**
3. **Process Transaction**
4. **End Process**

A lógica de negócio gira em torno de:
- localizar empresas/obrigações elegíveis no Backoffice;
- buscar os dados completos da empresa;
- buscar despesas da empresa;
- gerar listas e arquivos de importação;
- abrir telas no sistema Domínio;
- ajustar fechamento/período de trabalho quando necessário;
- importar os arquivos;
- verificar o relatório da importação;
- integrar as despesas importadas de volta no Backoffice;
- tratar sucesso, business exception e system exception.

---

## 4. Grafo de chamadas entre workflows

```text
Main.xaml
├── InitAllSettings.xaml
├── KillAllProcesses.xaml
├── GetTransactionData.xaml
│   └── GetFullCompany.xaml
├── Process.xaml
│   ├── GetExpenses.xaml
│   ├── GenerateImportationLists.xaml
│   ├── GenerateImportationArchives.xaml
│   ├── AbreOFechamento.xaml
│   ├── AlteraPeriodoDeTrabalho.xaml
│   ├── VerificacaodaImportacao.xaml
│   │   └── DesejaGravarOsDadosNotification.xaml
│   └── IntegrateAllExpenses.xaml
│       └── IntegrateExpense.xaml
├── SetObligationStatus.xaml
│   ├── TakeScreenshot.xaml
│   ├── RetryCurrentTransaction.xaml
│   └── KillAllProcesses.xaml
└── CloseAllApplications.xaml
    └── KillAllProcesses.xaml
```

---

## 5. Sequência macro de execução do `Main.xaml`

## 5.1 Initialization

### 5.1.1 Primeira execução
Se `Config == null`, o robô entra no bloco de inicialização real.

A sequência é:
1. registra em log a resolução da tela principal;
2. invoca `InitAllSettings.xaml`;
3. recebe e popula o dictionary `Config`;
4. valida e normaliza variáveis de configuração;
5. define o range de datas de despesas (`getExpenses_dateStart` e `getExpenses_dateEnd`);
6. busca as transações a processar;
7. conecta no banco de dados do Domínio;
8. zera `TransactionNumber`;
9. fecha processos/telas remanescentes via `KillAllProcesses.xaml`;
10. adiciona `businessProcessName` aos log fields.

### 5.1.2 Definições e normalizações feitas no `Main`
O `Main.xaml` contém validações adicionais depois da leitura do Excel:

- se `obligationDateStart` e `obligationDateEnd` não existirem, ele define um range padrão;
- se `obligationFinished` e `obligationIntegrated` estiverem ausentes, define valores default;
- se `getExpenses_dateStart` e `getExpenses_dateEnd` estiverem ausentes, calcula datas automaticamente.

Pelo XAML, quando não há datas explícitas para despesas, o fluxo calcula datas e grava em variáveis globais. O comportamento observado é orientado para trabalhar com um **range relativo ao mês atual**.

### 5.1.3 Busca das empresas/transações
O `Main.xaml` impõe a regra:

- **ou** existe `serviceCode`
- **ou** existe `obligationCode`
- **nunca os dois ao mesmo tempo**
- **nunca ambos vazios**

Se a configuração estiver inválida, o fluxo lança exceção com a mensagem:
> “Deve haver ao menos uma das opções de 'obligationCode' e 'serviceCode' no config. Também, não deve haver as duas ao mesmo tempo.”

#### Cenário A — serviço
Quando existe `serviceCode` e `obligationCode` está vazio:
- chama a activity customizada `GetServices`;
- consulta o endpoint de serviços;
- retorna uma lista inicial em `GlobalVariables.Companies`;
- percorre a lista e monta uma nova coleção deduplicada contendo o campo `id` da empresa a partir de `company_id`.

#### Cenário B — obrigação
Quando `serviceCode` está vazio e existe `obligationCode`:
- chama a activity customizada `GetObligation`;
- consulta o endpoint de obrigações;
- filtra por:
  - `authorization`
  - `obligationDebugCnpj`
  - `obligationDebugCompanyId`
  - `obligationDateStart`
  - `obligationDateEnd`
  - `obligationIntegrated`
  - `obligationCode`
  - `obligationFinished`
- retorna a coleção de empresas/obrigações em `GlobalVariables.Companies`.

Depois disso, o fluxo registra em log a quantidade e o conteúdo serializado da lista de empresas elegíveis.

### 5.1.4 Conexão com banco
Após montar a lista de transações:
- abre conexão com banco usando:
  - `databaseProviderName`
  - `databaseConnectionString`
- guarda a conexão em `GlobalVariables.databaseConnection`.

### 5.1.5 Controle de falhas consecutivas
Ainda na inicialização, o `Main.xaml` checa se o limite de exceções consecutivas de sistema foi ultrapassado. Se ultrapassou, lança exceção e termina.

### 5.1.6 Inicialização de aplicações
O `Main.xaml` contém uma chamada para `InitAllApplications.xaml`, porém ela está dentro de um **`CommentOut` com o rótulo “ATIVAR DEPOIS”**. Na versão analisada, essa inicialização **não é executada a partir do Main**.

---

## 5.2 Get Transaction Data

No estado **Get Transaction Data**, o fluxo faz:

1. verifica stop signal do Orchestrator (`ShouldStop`);
2. se houver solicitação de parada, define `EndProcess = True`;
3. se não houver stop signal, verifica se ainda existe item na lista (`GlobalVariables.Companies.Count > TransactionNumber`);
4. se existir item:
   - chama `GetTransactionData.xaml`;
5. se não existir:
   - zera/limpa `TransactionItem`;
   - registra mensagem de “sem mais transações”;
   - segue para fim do processo.

Se ocorrer erro ao obter a transação:
- faz log do erro;
- marca fim do processo.

---

## 5.3 Process Transaction

No estado **Process Transaction**, o fluxo:

1. zera `BusinessException`;
2. invoca `Process.xaml` com:
   - `in_Config`
   - `in_TransactionItem`
3. se o processamento for concluído:
   - tenta integrar/atualizar status da obrigação como sucesso;
4. se ocorrer `BusinessRuleException`:
   - tenta integrar/atualizar status como business exception;
5. se ocorrer system exception:
   - chama `SetObligationStatus.xaml` para tratamento de falha sistêmica.

As três transições de saída do estado incrementam `TransactionNumber`:
- Success
- Business Exception
- System Exception

Ou seja: após cada item processado, o índice anda para o próximo item.

---

## 5.4 End Process

No estado **End Process**:
1. tenta executar `CloseAllApplications.xaml`;
2. se falhar, faz log e executa `KillAllProcesses.xaml`;
3. se `SystemException` estiver preenchida, termina o workflow com falha;
4. caso contrário, finaliza normalmente.

---

## 6. Relatório detalhado por XAML

## 6.1 `InitAllSettings.xaml`

### Função
Montar o dictionary `Config`.

### Entradas principais
- `in_ConfigFile`
- `in_ConfigSheets`
- saída `out_Config`

### Sequência
1. inicia `out_Config`;
2. encerra processo `Excel`;
3. percorre as sheets configuradas;
4. lê cada sheet com `ReadRange`;
5. para cada linha não vazia:
   - adiciona par `Name`/`Value` ao dictionary;
6. depois lê a sheet `Assets`;
7. tenta buscar cada asset no Orchestrator;
8. quando o asset é recuperado, sobrescreve o valor no `Config`.

### Regra importante
**Assets sempre sobrescrevem Settings/Constants/ProcessVariables** quando possuem o mesmo nome lógico.

### Como o `Main.xaml` invoca este workflow
O `Main.xaml` passa explicitamente:
- `in_ConfigFile = Data\Config.xlsx`
- `in_ConfigSheets = ["Settings", "Constants", "ProcessVariables"]`

Ou seja, **na execução real observada, o dictionary `Config` é formado por essas 3 sheets + Assets**.

---

## 6.2 `InitAllApplications.xaml`

### Função
Inicializar aplicações.

### Ação identificada
- log de inicialização;
- chamada da activity customizada `LoginToDominioSystem`.

### Observação
Apesar de existir, a invocação dentro do `Main.xaml` está comentada/desativada na versão analisada.

---

## 6.3 `KillAllProcesses.xaml`

### Função
Encerrar processos do ambiente.

### Ação identificada
- log “Kill processes”.

### Observação crítica
O bloco que efetivamente contém `Kill Process` está dentro de um **`CommentOut` (“ATIVAR DEPOIS”)**.  
Na prática, nesta versão, o workflow **registra log, mas não evidencia execução real do encerramento de processos**.

---

## 6.4 `CloseAllApplications.xaml`

### Função
Encerramento normal das aplicações.

### Ação identificada
- log de fechamento;
- invoca `KillAllProcesses.xaml`.

### Implicação
Como `KillAllProcesses.xaml` está praticamente vazio/comentado, o fechamento normal também fica reduzido.

---

## 6.5 `GetTransactionData.xaml`

### Função
Recuperar a empresa corrente da lista e enriquecer com dados completos.

### Sequência
1. loga início da obtenção do item;
2. entra em `RetryScope`;
3. inicializa `out_jsonCompany = null`;
4. chama `GetFullCompany.xaml` passando o `id` de `GlobalVariables.Companies[io_TransactionNumber]`;
5. condição de sucesso do retry:
   - `out_jsonCompany != null`

### Em caso de falha
- loga a exceção;
- incrementa `io_TransactionNumber`.

### Saída relevante
- `out_TransactionItem` recebe o JSON completo da empresa.

---

## 6.6 `GetFullCompany.xaml`

### Função
Buscar os dados completos da empresa no Backoffice.

### Sequência
1. executa `HTTP Request`;
2. envia header `Authorization`;
3. envia parâmetro `company_id`;
4. aplica retry para status HTTP transitórios (timeout, 429, 500, 502, 503, 504);
5. se `StatusCode == OK`:
   - desserializa o JSON;
6. senão:
   - lança exceção;
7. atribui resultado a `out_json_company`.

### Papel no fluxo
Esse workflow transforma um item simplificado da lista inicial em um objeto completo da empresa, que será usado no `Process.xaml`.

---

## 6.7 `Process.xaml`

Este é o núcleo do processamento de cada item.

### Entradas principais
- `in_Config`
- `in_TransactionItem`
- `out_IntegrationNote`

### Sequência detalhada

#### 6.7.1 Início
1. log de início do processamento;
2. log de identificação da empresa;
3. inicializa `out_integrationNote`.

#### 6.7.2 Variáveis e pastas
1. define variáveis de processo;
2. monta caminhos de pasta com base em `mainPath` e no mês corrente;
3. cria pastas necessárias para:
   - arquivos de importação;
   - registros/prints de erro.

#### 6.7.3 Validações de negócio
Monta uma string de compilação de falhas de negócio.  
Se faltar informação obrigatória, lança `BusinessRuleException`.

Pelo fluxo, esse bloco verifica especialmente ausência de informações essenciais da empresa, como por exemplo código do Domínio.

#### 6.7.4 Geração dos dados para importação
Invoca, em ordem:
1. `GetExpenses.xaml`
2. `GenerateImportationLists.xaml`
3. `GenerateImportationArchives.xaml`

#### 6.7.5 Verificação de datas no Domínio
Consulta no banco as datas configuradas no sistema Domínio e define dois flags:

- `alterClosingPeriod`
- `alterWorkPeriod`

Esses flags controlam se será necessário:
- abrir fechamento;
- alterar período de trabalho.

#### 6.7.6 Operação no Domínio

##### Fechamento
Se `alterClosingPeriod == true`:
- registra log;
- chama `AbreOFechamento.xaml`.

##### Período de trabalho
Se `alterWorkPeriod == true`:
- registra log;
- chama `AlteraPeriodoDeTrabalho.xaml`.

##### Importação
1. abre a tela de importação via menu/pesquisa;
2. define:
   - Tipo = `T`
   - Conjunto de dados = `x`
3. abre seletor de arquivos;
4. cola o caminho da pasta de importação;
5. seleciona “Todos”;
6. confirma;
7. clica em “Importar”.

##### Verificação do relatório
Depois da importação chama:
- `VerificacaodaImportacao.xaml`

##### Integração pós-importação
Em seguida chama:
- `IntegrateAllExpenses.xaml`

---

## 6.8 `GetExpenses.xaml`

### Função
Buscar as despesas da empresa no Backoffice.

### Saídas
- `out_allExpenses`
- `out_getExpenses_dateStart`
- `out_getExpenses_dateEnd`

### Regra de datas
Se `getExpenses_dateStart` e `getExpenses_dateEnd` vierem no `Config`, o workflow usa essas datas.  
Se não vierem:
- `dateStart` = primeiro dia do mês atual menos 2 meses;
- `dateEnd` = primeiro dia do mês atual menos 1 mês.

### Request
Monta request com:
- `expensesUrl`
- `Authorization`
- `date_start`
- `date_end`
- `cnpj`
- `per_page`

### Tratamento
- se HTTP 200:
  - desserializa `company_expenses`;
  - se houver itens, retorna lista;
  - se não houver, retorna `null`;
- se não for 200:
  - lança exceção técnica;
- se a lista vier vazia/nula:
  - loga mensagem de ausência de despesas;
  - lança `BusinessRuleException`.

### Observação técnica importante
No XAML analisado há uma expressão de timeout que aparenta estar invertida:

```csharp
!string.IsNullOrEmpty(in_Config["getExpenses_timeout"].ToString()) ? "60000" : in_Config["getExpenses_timeout"].ToString()
```

Na prática, quando o config está preenchido, a expressão força `"60000"`.  
Quando está vazio, tenta usar o valor vazio. Isso merece revisão.

---

## 6.9 `GenerateImportationLists.xaml`

### Função
Limpar, separar e agrupar as despesas em listas prontas para virar arquivos de importação.

### Regras observadas

#### 6.9.1 Remoção de contas de débito proibidas
Usa `especificExceptDebitAccounts` para remover despesas cuja conta de débito não deve ser importada.

Ao remover, o workflow acrescenta observações em `io_integrationNote` informando que a despesa não foi importada.

#### 6.9.2 Remoção de despesas sem conta de débito
Percorre as despesas e também exclui as que não possuem conta de débito válida, igualmente registrando no `integrationNote`.

#### 6.9.3 Validação final
Se não restarem despesas válidas para importação, o workflow lança `BusinessRuleException` com a ideia de:
- “Não existem despesas válidas para importação”.

#### 6.9.4 Agrupamento em listas
Depois da limpeza:
- lê `especificDebitAccount`;
- separa subconjuntos específicos por conta de débito;
- monta `out_companiesLists`, que é uma lista de listas (`List<List<JToken>>`).

A intenção é evitar erros do Domínio gerando arquivos menores e específicos por conta.

#### 6.9.5 Naming lógico dos grupos
Cada sublista recebe um `fileName` via propriedades presentes nos próprios tokens, para uso posterior na geração dos `.txt`.

---

## 6.10 `GenerateImportationArchives.xaml`

### Função
Gerar os arquivos texto de importação.

### Sequência
Para cada lista de despesas:
1. monta uma string de linhas, uma por despesa;
2. formata cada linha como:
   - código Domínio da empresa;
   - vencimento formatado `dd/MM/yyyy`;
   - conta crédito;
   - conta débito;
   - valor com vírgula decimal;
   - histórico;
   - observação/nota higienizada (remove quebras de linha);
3. monta o nome do arquivo;
4. grava `.txt` no diretório de importação.

### Formato da linha
O padrão identificado é:

```text
dominio_code;vencimento;conta_credito;conta_debito;valor;historico;nota
```

---

## 6.11 `AbreOFechamento.xaml`

### Função
Abrir/ajustar fechamento no sistema Domínio.

### Sequência
1. abre menu `Controle > Fechamento`;
2. aguarda a tela de fechamento;
3. informa “Próximo fechamento”;
4. clica em “Gravar”;
5. se aparecer confirmação de gravação do fechamento/reabertura:
   - clica em `OK`.

---

## 6.12 `AlteraPeriodoDeTrabalho.xaml`

### Função
Abrir/ajustar período de trabalho.

### Sequência
1. loga as datas a serem aplicadas;
2. abre `Controle > Período de Trabalho`;
3. trata aviso “Este período de trabalho está fechado”;
4. preenche data inicial;
5. preenche data final;
6. entra em `Do While` clicando em `Gravar` até que a tela deixe de indicar permanência no estado anterior.

---

## 6.13 `VerificacaodaImportacao.xaml`

### Função
Analisar o relatório exibido pelo Domínio após a importação.

### Sequência principal
1. verifica se existe o relatório de importação;
2. se não existir:
   - lança exceção `ImportationReportDoNotExist`.

Se existir, analisa 3 blocos:

### 6.13.1 Erros
- lê atributo da aba/indicador de erros;
- se houver erros:
  - loga mensagem;
  - abre a aba;
  - tira screenshot;
  - salva a imagem;
  - cancela;
  - trata o popup “Deseja gravar os dados?” chamando `DesejaGravarOsDadosNotification.xaml`;
  - fecha a tela.

### 6.13.2 Críticas de estrutura
- lê atributo da aba/indicador;
- se houver críticas:
  - loga mensagem;
  - abre a aba;
  - tira screenshot;
  - salva a imagem;
  - fecha popup com `Esc`;
  - trata “Deseja gravar os dados?”;
  - fecha a tela;
  - monta `exceptionMessage`.

Esse é um ponto claramente tratado como situação relevante de falha/importação incompleta.

### 6.13.3 Advertências
- se houver advertências:
  - loga mensagem;
  - clica em “Todos”;
  - clica em “Gravar”;
  - se aparecer “Dados gravados com sucesso”:
    - incrementa `io_integrationNote`;
    - clica em `OK`;
  - fecha a tela.

### Resultado prático
Esse workflow pode:
- apenas registrar sucesso;
- registrar warnings no `integrationNote`;
- capturar screenshots;
- ou lançar exceção quando não encontra o relatório / encontra críticas estruturais relevantes.

---

## 6.14 `DesejaGravarOsDadosNotification.xaml`

### Função
Fechar popup “Deseja gravar os dados?”.

### Sequência
1. verifica se o popup existe;
2. se existir:
   - clica em `Não`;
   - clica em `Fechar`.

---

## 6.15 `IntegrateAllExpenses.xaml`

### Função
Integrar no Backoffice as despesas importadas.

### Regra principal
Executa somente se:
- `enableExpensesIntegration == true`

### Sequência
1. inicializa variáveis;
2. percorre cada lista de despesas;
3. percorre cada despesa individual;
4. chama `IntegrateExpense.xaml`;
5. se alguma integração individual falhar:
   - faz log da exceção;
   - continua o loop.

### Se desabilitado
Quando `enableExpensesIntegration == false`, apenas registra log informando que a integração está desabilitada.

---

## 6.16 `IntegrateExpense.xaml`

### Função
Marcar uma despesa específica como integrada.

### Sequência
1. monta `integrateExpenseUrl`;
2. faz `HTTP Request`;
3. se a resposta for positiva:
   - registra status em log;
4. senão:
   - lança exceção.

---

## 6.17 `SetObligationStatus.xaml`

### Função
Atualizar o status da obrigação/transação e centralizar o tratamento final do item.

### Estrutura
É um **Flowchart** com 3 ramos:
1. Success
2. Business Exception
3. System Exception

### 6.17.1 Success
- entra em retry scope;
- tenta integrar o status de sucesso.

### Observação crítica
O bloco que efetivamente integra a obrigação no cenário de sucesso está em **`CommentOut`**.  
Ou seja: a estrutura existe, mas a implementação efetiva de update está desativada no XAML analisado.

### 6.17.2 Business Exception
- tenta registrar o status de business exception.

### Observação crítica
Novamente, o bloco principal de integração do status também está em **`CommentOut`**.

### 6.17.3 System Exception
Esse é o ramo mais completo e ativo.

A sequência é:
1. loga falha/consecutivas;
2. monta `QueryRetry`;
3. tenta tirar screenshot via `TakeScreenshot.xaml`;
4. tenta montar `discord_message`;
5. envia screenshot para Discord via activity customizada;
6. tenta integrar o status técnico da obrigação;
7. incrementa contador de exceções consecutivas;
8. chama `RetryCurrentTransaction.xaml`;
9. remove log fields;
10. tenta matar processos via `KillAllProcesses.xaml`.

### Observações críticas
- o envio para Discord está ativo;
- a construção da mensagem para Discord usa:
  - identificação do processo;
  - identificação da empresa;
  - mensagem da exceção;
- o trecho **“INTEGRAÇÃO DA OBRIGAÇÃO”** no ramo de system exception também está em `CommentOut`;
- existe ainda um bloco comentado relacionado a `RetryNo`.

Resumo: este workflow está muito mais forte em **telemetria de erro / retry / screenshot / limpeza** do que em **persistir status de obrigação**, porque vários trechos de integração estão desativados.

---

## 6.18 `RetryCurrentTransaction.xaml`

### Função
Controlar retry local do item.

### Regras observadas
1. se `MaxRetryNumber > 0`, o item pode ser reprocessado localmente;
2. se `io_RetryNumber >= MaxRetryNumber`:
   - reseta retry;
   - incrementa `TransactionNumber`;
3. se ainda não atingiu o máximo:
   - incrementa retry local;
4. se não houver retry configurado:
   - incrementa `TransactionNumber` diretamente.

Esse workflow decide se o fluxo:
- tenta novamente o mesmo item; ou
- avança para o próximo.

---

## 6.19 `TakeScreenshot.xaml`

### Função
Tirar e salvar screenshot de erro.

### Sequência
1. executa screenshot;
2. se o caminho não veio preenchido:
   - inicializa caminho padrão;
3. cria pasta caso não exista;
4. salva imagem;
5. registra log.

---

## 7. Sequência linear consolidada do processamento de uma empresa

Abaixo está a sequência mais útil para gerar um fluxograma da empresa/unidade transacional:

1. `Main` inicializa config e variáveis.
2. `Main` busca empresas elegíveis por `serviceCode` ou `obligationCode`.
3. `Main` conecta no banco e zera índice.
4. `Main` entra no loop de transações.
5. `GetTransactionData` pega o item atual da lista.
6. `GetFullCompany` busca dados completos da empresa.
7. `Process` inicia o item.
8. `Process` valida se a empresa possui informações mínimas.
9. `GetExpenses` busca despesas no Backoffice.
10. `GenerateImportationLists` remove despesas inválidas e separa grupos.
11. `GenerateImportationArchives` grava arquivos `.txt`.
12. `Process` consulta no banco as datas/configurações do Domínio.
13. Se necessário, `AbreOFechamento`.
14. Se necessário, `AlteraPeriodoDeTrabalho`.
15. `Process` abre a tela de importação no Domínio.
16. `Process` informa o caminho da pasta dos arquivos.
17. `Process` executa a importação.
18. `VerificacaodaImportacao` analisa:
    - erros;
    - críticas estruturais;
    - advertências.
19. `IntegrateAllExpenses` percorre as despesas importadas.
20. `IntegrateExpense` marca cada despesa como integrada no Backoffice.
21. `Main` trata status do item:
    - sucesso;
    - business exception;
    - system exception.
22. `Main` incrementa `TransactionNumber`.
23. Se houver próximo item, repete.
24. Se não houver, encerra o processo.

---

## 8. Origem do `Config` e detalhamento do `Config.xlsx`

## 8.1 Como o dictionary `Config` é montado

Na execução real observada:
1. `Main.xaml` chama `InitAllSettings.xaml`;
2. informa o arquivo `Data\Config.xlsx`;
3. informa as sheets:
   - `Settings`
   - `Constants`
   - `ProcessVariables`
4. depois `InitAllSettings` lê `Assets`;
5. valores de assets do Orchestrator sobrescrevem valores anteriores se a chave for a mesma.

Portanto, a ordem lógica é:

```text
Settings
→ Constants
→ ProcessVariables
→ Assets (sobrescrevem)
```

---

## 8.2 Inventário das chaves do `Config.xlsx`

### Settings
- **businessProcessName** = `LancamentoDeDespesas` — Nome do processo em questão.
- **enableIntegration** = `True` — Configuração de integração das obrigações, deve ser "false" ou "true",                                                 se for true, as obrigações do processo serão integradas normalmente, se for false as obrigações não serão integradas
- **enableExpensesIntegration** = `False`
- **serviceCode** = `null` — Código do serviço no Backoffice
- **obligationCode** = `importacao_despesas` — Código da obrigação no Backoffice
- **obligationDateStart** = `20/03/2026` — Data inicial do range de datas que deve abranger a data de vencimento da obrigação em questão, se não estiver definida nenhuma data nessa variável, o processo define o primeiro e o ultimo dia do mês como as datas de inicio e fim do range.
- **obligationDateEnd** = `05/04/2026` — Data final do range de datas que deve abranger a data de vencimento da obrigação em questão, se não estiver definida nenhuma data nessa variável, o processo define o primeiro e o ultimo dia do mês como as datas de inicio e fim do range.
- **obligationFinished** = `True`
- **obligationIntegrated** = `False`
- **obligationDebugCnpj** = `null` — Configuração para debug. Se for passado o CNPJ de uma empresa, o processo será feito apenas na empresa em questão
- **obligationDebugCompanyId** = `null` — Configuração para debug. Se for passado o ID de uma empresa, o processo será feito apenas na empresa em questão
- **discordIntegrationWebhook** = `[REDACTED_WEBHOOK]` — Webhook para integração e envio de mensagens e prints quando a automação falhar.
- **discordIntegrationProcessIdentificationMessage** = `Houve um erro durante o seguinte processo: `
- **discordIntegrationCompanyIdentificationMessage** = `O erro ocorreu na seguinte empresa:`

### Constants
- **MaxRetryNumber** = `1` — Must be 0 if working with Orchestrator queues. If > 0, the robot will retry the same transaction which failed with a system exception. Must be an integer value.
- **MaxConsecutiveSystemExceptions** = `3` — The number of consecutive system exceptions allowed. If MaxConsecutiveSystemExceptions is reached, the job is stopped. To disable this feature, set the value to 0.
- **ExScreenshotsFolderPath** = `Exceptions_Screenshots` — Where to save exceptions screenshots - can be a full or a relative path.
- **RetryNumberGetTransactionItem** = `15` — The number of times Get Transaction Item activity is retried in case of an exception. Must be an integer >= 1.
- **RetryNumberSetTransactionStatus** = `2` — The number of times Set transaction status activity is retried in case of an exception. Must be an integer >= 1.
- **login_at_domonio_timeout** = `30000` — Tempo em milissegundos que a automação deve esperar para o carregamento total do sistema domínio no ambiente em questão
- **alter_company_timeout** = `20000` — Tempo em milissegundos que a automação deve esperar para o domínio carregar a empresa no ambiente
- **defaultDelayBefore** = `0` — Delay before padrão das atividades do processo
- **defaultDelayAfter** = `0` — Delay after padrão das atividades do processo
- **LogMessage_Success** = `Processo finalizado com sucesso!` — Mensagem de integração da obrigação no backoffice quando o processo for bem sucedido.
- **LogMessage_SuccessOpenWorkPeriod** = `Periodo de trabalho aberto com sucesso!`
- **LogMessage_SuccessArcriveRead** = `Arquivo de importação lido com sucesso!`
- **LogMessage_NoMoreTransactionItems** = `Sem mais empresas para iterar sobre, finalizando o processo!` — Mensagem de finalização de iteração com as obrigações disponiveis.
- **LogMessage_Transition_NewTransaction** = `Processando a transação numero: ` — Mensagem apresentada quando ocorre a transição de nome New Transaction indo do Get Transaction Data para o Process Transaction
- **LogMessage_EnableIntegration** = `Opção 'enable_integration' está desabilitada, o processo não irá integrar as obrigações.`
- **LogMessage_NoExpensesToEspecificatedDebitAccount** = `Não existem despesas com a conta de débito especificada: `
- **LogMessage_OpenWorkPeriod** = `Abrindo periodo de trabalho...`
- **LogMessage_OpenClosing** = `Abrindo periodo de fechamento...`
- **LogMessage_ImportationError** = `Houve um erro no momento da importação!` — Quando, no momento de apresentação dos relatórios, após ler os arquivos de importação, é apresentado um erro.
- **LogMessage_ImportationStructuralCritiques** = `Foram apresentadas criticas às estruturas dos arquivos txt de importação.` — Quando, no momento de apresentação dos relatórios, após ler os arquivos de importação, é apresentado um erro.
- **LogMessage_ImportationAdvertencies** = `Existem advertências relativas aos arquivos de importação.` — Quando, no momento de apresentação dos relatórios, após ler os arquivos de importação, é apresentada a tela de advertências. É necessário selecionar todos os registros à serem importados.
- **LogMessage_EnableExpensesIntegration** = `Opção 'enableExpensesIntegration' está desabilitada, o processo não irá integrar as obrigações.`
- **ExceptionMessage_GetTransactionData** = `Error no get company para a empresa: ` — Static part of logging message. Error retrieving Transaction Data.
- **ExceptionMessage_Application** = `Houve uma exceção no processo principal.` — Mensagem de erro apresentada quando ocorre um erro inesperado no processo principal
- **ExceptionMessage_CompanyWithoutDominioCode** = `Empresa não possui código do Domínio! Favor verificar cadastro no backoffice.`
- **ExceptionMessage_CompanyWithoutFoundationDate** = `Empresa não possui data de fundação! Favor verificar cadastro no backoffice.`
- **ExceptionMessage_ConsecutiveErrors** = `The maximum number of consecutive system exceptions was reached. ` — Error message in case MaxConsecutiveSystemExceptions number is reached.
- **ExceptionMessage_LackOfInformation** = `Faltam informações para a execução da automação na presente empresa. Favor verificar.`
- **ExceptionMessage_AnyExpense** = `Não existem despesas na empresa em questão para o periodo determinado.` — Caso não existam despesas na empresa em questão.
- **ExceptionMessage_ExpenseRequest** = `Erro no processo de requisição das despesas - Get Expenses.` — Caso haja um erro no processo de Get Expenses
- **ExceptionMessage_ExpenseDatesDefinition** = `Erro ao definir as datas de inicio e final do Get Exepenses. As datas devem estar com o tipo "texto" na planilha "Con...` — Caso haja um erro durante o processo de definição de datas do request das despesas.
- **ExceptionMessage_StructuralCritiquesToImportationArchives** = `Foram exibidas criticas de estruturas aos txts de importação no processo de importação. Favor verificar`
- **ExceptionMessage_ImportationReportDoNotExist** = `O relatorio de importações do Domínio não foi apresentado.`

### Assets
- **dominio_username** → asset do Orchestrator `shared` — Username para o login no sistema domínio
- **dominio_password** → asset do Orchestrator `shared` — Password para o login no sistema domínio
- **authorization** → asset do Orchestrator `shared` — Token para request ao backoffice
- **counter_code** → asset do Orchestrator `shared` — Codigo do contador dentro do domínio
- **database_connection_string** → asset do Orchestrator `shared` — String de conexão com o banco de dados do Domínio
- **database_provider_name** → asset do Orchestrator `shared` — Provider name para conexão no banco de dados do Domínio

### ProcessVariables
- **mainPath** = `\\192.168.8.8\Razonet\18- OPERAÇÃO RAZONET\Robo de despesas` — Endereço onde serão alocados os arquivos de importação e relatórios de erros de importação do processo
- **allDebitAccountImportationArchiveName** = `ContasDeDebitoGeral`
- **especificDebitAccount** = `2055` — O Domínio pode apresentar erro de acordo com a quantidade de despesas inseridas em um determinado arquivo de importação. Por isso, existem as contas de débito específicas que devem estar separadas por arquivo de importação.Deve estar no fomraco "contaDeDebito,contaDeDebito,contaDeDebito"
- **especificExceptDebitAccounts** = `2110,2074,1204,2103,2112,6` — Contas de débito que não devem ser importadas para o Domínio.
- **dateEndWorkPeriod** = `01/01/2030` — Data final do periodo de trabalho cadastrado no sistema Domínio.
- **attribute_invisibleElement** = `não disponível` — Atributo que é apresentado quando um determinado elemento é apresentado na tela, mas não está disponivel para iteração.
- **expensesUrl** = `https://app.razonet.com.br/integration/v1/company_expenses` — Url para requisição das despesas no backoffice.
- **getExpenses_timeout** = `60000` — Timeout do request.
- **getExpenses_perPage** = `3000` — Numero de despesas apresentadas em cada página da paginação do json.
- **getExpenses_contentType** = `application/json` — Tipo de conteúdo que será aceitado no request
- **getExpenses_dateStart** = `null` — Inicio do range de datas que abrange a data de vencimento das despesas que irão ser importadas. Deve estar no formato "dd/MM/yyyy" e só deve existir caso o getExpenses_dateEnd existir também.
- **getExpenses_dateEnd** = `null` — Fim do range de datas que abrange a data de vencimento das despesas que irão ser importadas. Deve estar no formato "dd/MM/yyyy" e só deve existir caso o getExpenses_dateStart existir também. Deve ser posterior ao getExpenses_dateStart
- **postExpenses_endPoint** = `set_as_integrated` — endpoint para request post para integrar as obrigações, o formato final do endpoint fica "expensesUrl + /:id/set_as_integrated
- **postExpenses_timeout** = `5000` — Timeout do request
- **postExpenses_contentType** = `application/json` — Content-type do post expenses


---

## 9. Mapeamento de chaves mais relevantes por etapa do fluxo

### Inicialização / seleção de transações
- `businessProcessName`
- `serviceCode`
- `obligationCode`
- `obligationDateStart`
- `obligationDateEnd`
- `obligationFinished`
- `obligationIntegrated`
- `obligationDebugCnpj`
- `obligationDebugCompanyId`
- `authorization`

### Banco / Domínio
- `databaseConnectionString`
- `databaseProviderName`
- `counterCode`
- `dominioLoginUsername`
- `dominioLoginPassword`

### Processo operacional
- `mainPath`
- `dateEndWorkPeriod`
- `attribute_invisibleElement`

### Busca de despesas
- `expensesUrl`
- `getExpenses_timeout`
- `getExpenses_perPage`
- `getExpenses_contentType`
- `getExpenses_dateStart`
- `getExpenses_dateEnd`

### Pós-importação de despesas
- `enableExpensesIntegration`
- `postExpenses_endPoint`
- `postExpenses_timeout`
- `postExpenses_contentType`

### Controle de retry
- `MaxRetryNumber`
- `MaxConsecutiveSystemExceptions`
- `RetryNumberGetTransactionItem`
- `RetryNumberSetTransactionStatus`

### Mensagens / logs / exceções
- `LogMessage_*`
- `ExceptionMessage_*`

### Discord / monitoramento
- `discordIntegrationWebhook`
- `discordIntegrationProcessIdentificationMessage`
- `discordIntegrationCompanyIdentificationMessage`

---

## 10. Pontos de atenção identificados

### 10.1 Blocos comentados/desativados
Existem trechos relevantes desativados no projeto analisado:

1. **`InitAllApplications` no `Main.xaml`**
   - presente, mas dentro de `CommentOut`.

2. **`KillProcess` em `KillAllProcesses.xaml`**
   - workflow existe, porém o encerramento real está comentado.

3. **Integração de status da obrigação em `SetObligationStatus.xaml`**
   - blocos de sucesso, business exception e parte do system exception estão comentados.

Esses pontos impactam diretamente qualquer fluxograma “as is”.  
Para um fluxograma fiel à versão atual, esses blocos devem aparecer como **etapas previstas, porém desativadas**.

### 10.2 Timeout de `GetExpenses`
A expressão de timeout aparenta estar invertida e merece revisão.

### 10.3 Forte dependência de activities customizadas
O projeto depende de várias activities customizadas e bibliotecas, como:
- `GetServices`
- `GetObligation`
- `LoginToDominioSystem`
- `SendDiscordScreenshot`

Para um fluxograma funcional, essas activities devem ser representadas como **caixas pretas externas** quando não houver código interno delas disponível.

### 10.4 Deduplicação no fluxo de serviços
No cenário de `serviceCode`, o fluxo reconstrói a lista de empresas com base em `company_id`.  
Isso é importante no fluxograma porque existe uma etapa intermediária de **normalização da coleção retornada**.

---

## 11. Sugestão de representação para a outra IA gerar fluxograma

Para a outra IA, vale considerar estes blocos macro:

1. **Inicialização**
2. **Carga de configuração**
3. **Busca das empresas**
4. **Loop por empresa**
5. **Busca de dados completos da empresa**
6. **Busca de despesas**
7. **Filtragem/limpeza das despesas**
8. **Geração dos arquivos**
9. **Ajustes de fechamento/período no Domínio**
10. **Importação**
11. **Validação do relatório**
12. **Integração das despesas**
13. **Tratamento de sucesso / business exception / system exception**
14. **Retry / avanço para próximo item**
15. **Encerramento**

---

## 12. Resumo final

O processo é um robô de importação de despesas com arquitetura REFramework, orientado a:
- selecionar empresas elegíveis no Backoffice;
- buscar despesas;
- gerar arquivos `.txt`;
- importar no Domínio;
- validar o resultado;
- integrar as despesas importadas.

O `Process.xaml` concentra a lógica operacional principal.  
O `Main.xaml` coordena o loop, retries, estado do item e encerramento.

Os pontos mais relevantes para o fluxograma são:
- decisão inicial entre `serviceCode` e `obligationCode`;
- loop de transações;
- branch de validações de negócio;
- branch de ajuste de fechamento/período;
- branch de validação do relatório de importação;
- branch de tratamento de exceções;
- retry local da transação;
- blocos comentados/desativados que precisam ser marcados como tal no diagrama.

