# Dataset — backup obrigatório (fora do Git)

O dataset bruto **não pode** ir para o Git/GitHub:

- ~33 GB de JSONL
- GitHub rejeita arquivos > 100 MB
- Repositório ficaria inviável

## O que ESTÁ no Git

- `UltronML/datasets/metadata.json` — versões de parquet exportadas
- `UltronML/datasets/dataset_sessions_inventory.json` — inventário de sessões (contagens, tamanhos)

## O que copiar manualmente antes de formatar o PC

```
C:\Users\alber\Documents\NinjaTrader 8\UltronV1\dataset\
```

Restaurar no mesmo caminho após reinstalar NinjaTrader.

## Regenerar inventário

```powershell
python scripts/_gen_dataset_inventory.py
```
