# Ultron V1 — Phase 0

Motor de order flow para **MNQ 09-26**. Fase 0: ingestão de ticks, processamento OF e log diagnóstico por barra.

## Estrutura

```
UltronV1/
├── UltronV1.Core/          # Engine sem dependência NinjaTrader
├── UltronV1.Tests/        # xUnit
├── UltronV1.Training/     # Fase 4 — ML platform (Python)
├── UltronV1.Validation/   # Fase 4.5 — Validation framework (Python)
├── NinjaTrader/
│   ├── UltronV1.cs           # Indicador NT8
│   └── UltronV1Paper.cs      # Fase 5 — paper outcome collection
└── UltronV1.sln
```

## Build (Core + Tests)

```powershell
cd UltronV1
dotnet build UltronV1.sln
dotnet test UltronV1.Tests
```

## Deploy no NinjaTrader 8

1. Build Release do Core:
   ```powershell
   dotnet build UltronV1.Core -c Release
   ```
2. Copie `UltronV1.Core\bin\Release\netstandard2.0\UltronV1.Core.dll` para:
   `Documents\NinjaTrader 8\bin\Custom\`
3. Copie `NinjaTrader\UltronV1.cs` para:
   `Documents\NinjaTrader 8\bin\Custom\Indicators\`
4. No NinjaScript Editor: **References** → adicionar `UltronV1.Core.dll` se necessário.
5. Compile (F5).
6. Aplique o indicador **UltronV1** em um chart **MNQ 09-26**, timeframe 5 min, **Calculate = On each tick**.

## Validação Phase 0

- Chart MNQ 09-26: Output Window exibe linhas `[UltronV1] Bar=... Delta=... Sweep=...`
- Chart ES ou MNQ contrato errado: mensagem de erro e engine desabilitado
- `dotnet test`: todos os testes passam

## Dataset Engine (Fase 3)

Formato primário: **JSON Lines** (não CSV) — uma oportunidade por linha, ML-ready.

```
Documents/NinjaTrader 8/UltronV1/dataset/
└── sessions/{sessionId}/
    ├── manifest.json
    ├── opportunities.jsonl    ← registro completo por barra
    └── outcomes.jsonl         ← enriquecimento pós-trade (Fase 5)
```

Ative no indicador: **Enable Dataset Engine = true**

Cada registro inclui: `signalId`, versões (ultron/featureSchema/score/config), features[40], gates, orderFlow, scoreBreakdown, explainText, shadow flags, decision.

Schema: `UltronV1.Core/Dataset/dataset_record_schema_v1.json`

## Fases

| Fase | Status |
|------|--------|
| 0 — Core + TickProcessor + Indicator skeleton | Concluída |
| 1 — FeatureEngine + gates + score + arrows | Concluída |
| 2 — OF confirm + Explain + HUD + NR Compare | Concluída |
| 3 — Dataset Engine + Shadow pipeline | Concluída |
| 4 — ML platform + ONNX + MlAdvisor | Concluída |
| 4.5 — Validation Framework | Concluída |
| **5 — Outcome Collection & Paper Execution** | **Atual** |
| 6 — Tier A calibration | Pendente (pós Gate L1) |

### Fase 5 — Outcome Collection & Paper Execution

Strategy paper para Replay / Sim101 — completa `outcomes.jsonl`. **Sem capital real.**

Ver [`Fase5-OutcomeCollection.md`](Fase5-OutcomeCollection.md)

### Fase 4.5 — Validation Framework

Projeto: `UltronV1.Validation/` — comparação quantitativa Ultron vs NightRanger **sem alterar lógica do engine**.

```powershell
cd UltronV1.Validation
pip install -r requirements.txt
python validate.py --synthetic --output ./reports
```

Ver `UltronV1.Validation/README.md` para uso com dados NT8 reais.

### Fase 4 — ML Platform

Projeto: `UltronV1.Training/` — ver `UltronV1.Training/README.md`
