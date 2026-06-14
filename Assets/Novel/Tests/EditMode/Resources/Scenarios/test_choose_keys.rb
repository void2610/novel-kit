choose(["A", "B"])                  # 自動採番 __choice_N（一時・セーブ除外）
choose(["A", "B"], key: :picked)    # 明示キー（永続・セーブ対象）
flag "kept", 1
