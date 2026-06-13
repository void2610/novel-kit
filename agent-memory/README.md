# agent-memory

Claude Code（このリポジトリで作業しているエージェント）の**プロジェクトメモリのスナップショット**。
別 PC への移行や、ローカルメモリ消失時の復元のためにリポジトリへコミットしている。

## これは何か

Claude Code のメモリは通常ローカルにあり git 管理されない:

```
~/.claude/projects/-Users-shuya-Documents-GitHub/memory/
```

（ディレクトリ名 `-Users-shuya-Documents-GitHub` は親フォルダ `/Users/shuya/Documents/GitHub` から導出される。
メモリは novel-kit 単体ではなく `~/Documents/GitHub` 配下の全プロジェクトで共有される。）

このフォルダの `.md` はその中身を**逐語コピー**したもの。

| ファイル | 役割 |
|---|---|
| `MEMORY.md` | メモリ索引（各メモリへの1行ポインタ） |
| `novel-kit-project.md` | novel-kit の要約・現状・確定判断のポインタ（`type: project`） |
| `okf-knowledge-base.md` | OKF 知識ベース運用の横断メモ（`type: reference`・全プロジェクト共通） |

> 注意: これらは**要約／ポインタ**であり、設計の一次情報は `Knowledge~/design/`（ADR 16 件ほか）にある。
> メモリが無くても `Knowledge~/design/index.md` → `decisions/index.md` から全設計判断を辿れる。

## 別 PC での復元手順

新しい PC でこのリポジトリを clone した後:

```bash
# 1. メモリ格納先を作る（パスは GitHub 親フォルダから導出される名前に合わせる）
mkdir -p ~/.claude/projects/-Users-shuya-Documents-GitHub/memory

# 2. スナップショットをコピー（README.md は除く）
cp agent-memory/MEMORY.md \
   agent-memory/novel-kit-project.md \
   agent-memory/okf-knowledge-base.md \
   ~/.claude/projects/-Users-shuya-Documents-GitHub/memory/
```

- 新 PC のユーザー名や GitHub フォルダの場所が異なる場合は、格納先ディレクトリ名を実環境に合わせて読み替える
  （`<path-to>/GitHub` → `-<path-to>-GitHub` のようにパス区切りを `-` に置換した名前になる）。
- `okf-knowledge-base.md` は全プロジェクト共通の参照メモなので、novel-kit に限らず流用してよい。

## 更新方針

このスナップショットは手動同期。メモリを更新したら、必要に応じてここへコピーし直してコミットする。
一次情報は常に `Knowledge~/design/` 側を正とする。
