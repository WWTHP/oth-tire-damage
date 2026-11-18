# oth-tire-damage

**TireDamage**: an [Over the Hill](docs/games/over-the-hill/README.md) MelonLoader mod. Big hits
damage tires; a tire pops when its health runs out (or ruptures on a huge hit, or after driving on
it flat). Popped and ruptured tires sit lower and grip less. Hold **F4** while stopped to repair:
a tool kit cosmetic (pipe wrench or hi-lift jack) patches popped tires as often as you like, each
mounted spare wheel replaces one tire. Houses, the mechanic and camp replace every tire and
restock the spares. Settings: the settings hub tab "Tires" (or F1 without the hub).

```bash
python3 -m venv .venv && .venv/bin/pip install -r requirements-dev.txt
tools/check.sh                            # all checks, no deploy
cd TireDamage && dotnet build -c Release  # build + deploy to the game
```

Details: [docs/project/README.md](docs/project/README.md). Agents: start at [AGENTS.md](AGENTS.md).
