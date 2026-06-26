# RC2 Fase 4A/4B — Verificação de Deploy NinjaTrader

**Data:** 26/06/2026  
**Escopo:** Sincronização workspace → `Documents\NinjaTrader 8\bin\Custom`  
**Engine / SignalEvaluator / FeatureEngine / PaperBarSim:** não alterados

---

## 1. Resumo executivo

| Item | Status |
|------|--------|
| Arquivos `.cs` RC2 copiados para Custom | ✅ |
| DLLs (`UltronML.Core`, ONNX, dependências) copiadas | ✅ |
| Referências em `NinjaTrader.Custom.csproj` | ✅ |
| Referências em `Config.xml` (NT8) | ✅ |
| Compilação `dotnet build` (Release, LangVersion=12) | ✅ **0 erros** |
| `NinjaTrader.Custom.dll` atualizado na raiz Custom | ✅ |
| Símbolos ML presentes no DLL | ✅ |
| Workspace == Custom (SHA256) | ✅ todos os arquivos Ultron |

**Ação manual restante:** No NinjaTrader aberto, pressionar **F5** (ou reiniciar NT8) para recarregar o assembly. Se o NT8 já estiver em execução, ele pode manter o DLL antigo em memória até recompilar.

---

## 2. Arquivos copiados

### 2.1 C# — Strategies

| Arquivo | Ação | SHA256 antes (Custom) | SHA256 depois |
|---------|------|----------------------|---------------|
| `Strategies\UltronV1Paper.cs` | **ATUALIZADO** | `E0EED803…` | `5A07BC1641B88A43DBFD720CD59E90F98C5C5E07F799A852C7E28A82D053E8F3` |

### 2.2 C# — Ultron\Shared (novos)

| Arquivo | Ação | SHA256 |
|---------|------|--------|
| `Ultron\Shared\UltronMlHost.cs` | COPIADO | `6B650851509F178E29F7CD9B93C7DB6ED74F0CF89960BCC9F63370BAAE28028C` |
| `Ultron\Shared\UltronMlFilterMode.cs` | COPIADO | `BBDA529DCACBC78E2C88FB25B40A2009D5F25285E41AE92F4DF21B02ABE3CA21` |
| `Ultron\Shared\UltronShadowFeatureBuilder.cs` | COPIADO | `DF7D0227867219252826B825642F2EBDEDE9FAB3FCB76C42496E626E57499D83` |
| `Ultron\Shared\UltronShadowPredictionSink.cs` | COPIADO | `7D0022F8B4394AD2CC788D80C281F7E6AA5E3263591CD4536CC4ECE331E1637C` |
| `Ultron\Shared\UltronShadowMlHost.cs` | COPIADO (legado 4A) | `89FE7532990FBB498A288D13B2A0BA102842D487885E7F98F5D4952BEC38EB2E` |

### 2.3 DLLs e nativos

| Arquivo | SHA256 |
|---------|--------|
| `UltronML.Core.dll` | `4886576A39FE26FFCCAAE945E45E007D251F461C21F2DFED395E9204BBFF44EF` |
| `Microsoft.ML.OnnxRuntime.dll` | `BD755E67FB8DED01E9BC6AE3F213648FB8F6464746D8C00E8869039E2A0FD885` |
| `onnxruntime.dll` (win-x64 native) | `B9DBA35EC85D49C0186332C19A01601AE598EDE701B79DCC70DE50D4DBA36FA6` |
| `onnxruntime_providers_shared.dll` | `BA645BF03BD60DF622CF27D6111D1359506A90D0E3B5558F0A271F0B560CF97C` |
| `System.Text.Json.dll` | `42A3D66464AA3F52202A625FB8D719663CEAE07678081253B54D6ED56A95EA0A` |
| `System.Text.Encodings.Web.dll` | `5AD1F348223F281C34B9B857445A1C3412A430026EF049AEAC641C53A92408BE` |
| `Microsoft.Bcl.AsyncInterfaces.dll` | `33E3C58D46C76D5535D7C24085957B7CCB7CE3815707300955A6309ECC2E81D1` |
| `System.Memory.dll` | `11590D8BB3B12F29F4202B3EF8593229A5CD6DEBB61E76CBA9AC5493A82EE382` |
| `System.Buffers.dll` | `C65FFF603B283DC966D1A8B730C11D5E5E750E8021BD24640612F6CC3F2C6FB7` |
| `System.Numerics.Vectors.dll` | `2324EE5A35674269225E2AA20957CE8830DBCA0CFFB918BD593F7A3222DEE480` |
| `System.Runtime.CompilerServices.Unsafe.dll` | `01748200F2400C742AA689F1F5101BD6298EFDFD92C00C18F4FA473847235BA9` |
| `System.Threading.Tasks.Extensions.dll` | `09901F63EBC4B8F0897B0128FB230C146529AEE5C5CC90D7A7872E66A976BB5F` |

**Origem `UltronML.Core.dll`:** build Release  
`UltronV1\UltronML\UltronML.Core\bin\Release\netstandard2.0\UltronML.Core.dll`

---

## 3. Arquivos de projeto atualizados

### 3.1 `NinjaTrader.Custom.csproj`

**Referências adicionadas:**

```xml
<Reference Include="UltronML.Core">
  <HintPath>...\Custom\UltronML.Core.dll</HintPath>
</Reference>
<Reference Include="Microsoft.ML.OnnxRuntime">
  <HintPath>...\Custom\Microsoft.ML.OnnxRuntime.dll</HintPath>
</Reference>
```

**`<Compile Include>` adicionados:**

- `Ultron\Shared\UltronMlFilterMode.cs`
- `Ultron\Shared\UltronMlHost.cs`
- `Ultron\Shared\UltronShadowFeatureBuilder.cs`
- `Ultron\Shared\UltronShadowPredictionSink.cs`
- `Ultron\Shared\UltronShadowMlHost.cs`

### 3.2 `Documents\NinjaTrader 8\Config.xml`

Entradas adicionadas em `<References>`:

- `*MyDocuments*\NinjaTrader 8\bin\Custom\UltronML.Core.dll`
- `*MyDocuments*\NinjaTrader 8\bin\Custom\Microsoft.ML.OnnxRuntime.dll`

---

## 4. SHA256 — `NinjaTrader.Custom.dll` (antes/depois)

| | Valor |
|---|-------|
| **Antes** (pré-4B, 26/06 11:19:54) | `2638382AD70A5BE51BB0F69D1CE461E11E986120CA0E441A0889EF73597F1BEB` |
| **Depois** (pós-deploy, 26/06 11:31:57) | `7CCA5316B7043D3E1C23E210F4756E02D48532A01810BABE23F8F6981140D2BB` |
| Tamanho antes | 1 821 184 bytes |
| Tamanho depois | 1 851 392 bytes |

**Nota:** O `dotnet build` gera o DLL em `Custom\bin\Release\`. O artefato foi **copiado** para `Custom\NinjaTrader.Custom.dll` (raiz), que é o caminho carregado pelo NinjaTrader em runtime.

---

## 5. Compilação

| Parâmetro | Valor |
|-----------|-------|
| Comando | `dotnet build NinjaTrader.Custom.csproj -c Release /p:LangVersion=12` |
| Resultado | **Sucesso — 0 erros** |
| Hora do DLL compilado | **26/06/2026 11:31:57** |
| Avisos | ~2797 (pré-existentes: conflitos CABABOM, CS0436, etc.) |

**Limitação do ambiente:** MSBuild/Visual Studio não instalado; `LangVersion=13.0` do `.csproj` não é suportado pelo `dotnet` SDK local — usado override `LangVersion=12` apenas na linha de comando. O NinjaTrader Editor (F5) usa o compilador Roslyn embutido e suporta LangVersion 13.

---

## 6. Validação de símbolos no `NinjaTrader.Custom.dll`

| Símbolo solicitado | Presente? |
|--------------------|-----------|
| `EnableShadowMl` | ✅ |
| `MlFilterMode` | ✅ |
| `EnableMlFilter` | ✅ |
| `ForcePassThrough` | ✅ |
| `MlUseAutoThreshold` | ✅ |
| `MlThresholdManual` | ✅ |
| `UltronMlHost` | ✅ |
| `UltronShadowPredictionSink` / `ShadowPrediction` | ✅ |
| `Shadow ML` (GroupName) | ✅ |
| `ML Filter` (GroupName) | ✅ |

---

## 7. Propriedades ML na Strategy (esperado após F5)

Grupo **Shadow ML:**

| Display Name | Propriedade |
|--------------|-------------|
| Enable Shadow ML | `EnableShadowMl` |
| Shadow ML Model Directory | `ShadowMlModelDirectory` |

Grupo **ML Filter:**

| Display Name | Propriedade |
|--------------|-------------|
| ML Filter Mode | `MlFilterMode` |
| Enable ML Filter | `EnableMlFilter` |
| Force Pass Through | `ForcePassThrough` |
| Use Auto Threshold | `MlUseAutoThreshold` |
| Manual Threshold | `MlThresholdManual` |

Todas possuem `[NinjaScriptProperty]` + `[Display]` no fonte sincronizado.

---

## 8. Sincronização workspace ↔ Custom

Verificação SHA256 em **26/06/2026** — todos **MATCH**:

- `UltronV1Paper.cs`
- Todos os 14 arquivos em `NinjaTrader/Shared/` (incluindo os 5 novos ML)

---

## 9. Correções mínimas de compilação (camada NinjaTrader apenas)

Durante o deploy, o código copiado **não compilava** sem dois ajustes na camada **NinjaTrader/Shared** (não Engine). Alterações aplicadas no **workspace e replicadas no Custom**:

| Arquivo | Problema | Correção |
|---------|----------|----------|
| `UltronShadowPredictionSink.cs` | `TradeOutcome` está em `UltronV1.Core.Dataset`, não `Execution` | `using UltronV1.Core.Dataset` |
| `UltronMlHost.cs` | Qualificador errado `Execution.TradeOutcome` | `UltronV1.Core.Dataset.TradeOutcome` |
| `UltronShadowMlHost.cs` | Idem | `UltronV1.Core.Dataset.TradeOutcome` |
| `UltronShadowFeatureBuilder.cs` | `SignalTier.APlus` não existe no enum Core | Removido case `APlus` do switch (enum Core: None, C, B, A) |

**Impacto em runtime:** nenhuma mudança de lógica operacional — apenas correção de namespace e referência a membro de enum inexistente que impedia compilação.

---

## 10. Checklist final

### Deploy (concluído)

- [x] Comparar workspace vs Custom
- [x] Copiar `UltronV1Paper.cs`
- [x] Copiar Shared ML (4A/4B)
- [x] Build + copiar `UltronML.Core.dll`
- [x] Copiar `Microsoft.ML.OnnxRuntime.dll` + nativos win-x64
- [x] Copiar dependências transitivas (`System.Text.Json`, etc.)
- [x] Atualizar `NinjaTrader.Custom.csproj` (Reference + Compile)
- [x] Atualizar `Config.xml` (References NT8)
- [x] Compilar sem erros
- [x] Atualizar `NinjaTrader.Custom.dll` na raiz Custom
- [x] Validar símbolos ML no DLL

### No NinjaTrader (ação do usuário)

- [ ] Abrir NinjaScript Editor
- [ ] Pressionar **F5** (Compile)
- [ ] Confirmar **0 erros** na janela de Output
- [ ] Remover e reaplicar strategy **UltronV1Paper** no chart
- [ ] Abrir Properties → confirmar grupos **Shadow ML** e **ML Filter**
- [ ] (Opcional) Habilitar **Enable Shadow ML** para validar `[ML FILTER]` no Output

### Pós-deploy operacional (RC2)

- [ ] Champion model em `Documents\NinjaTrader 8\UltronV1\UltronML\models\champion\`
- [ ] Shadow mode 1–2 semanas antes de Active

---

## 11. Conclusão

O deploy RC2 Fase 4A/4B está **completo** na pasta Custom. O `NinjaTrader.Custom.dll` compilado contém todas as propriedades e tipos ML. O workspace e o Custom estão **sincronizados** (SHA256 idênticos).

Após **F5** no NinjaTrader, a strategy `UltronV1Paper` deve exibir os grupos **Shadow ML** e **ML Filter** com todas as propriedades listadas na seção 7.

---

*RC2 Deploy Verification — 26/06/2026*
