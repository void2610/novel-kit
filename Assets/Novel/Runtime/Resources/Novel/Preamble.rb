# novel-kit 共通 Ruby ヘルパ。作家が say/choose 等の素直な関数でシナリオを書けるようにする。
# 実体は VitalRouter.MRuby の cmd 発行で、ヘルパ名は NovelScenarioRunner の AddCommand 登録名に対応する。
# RubyScriptedImporter がサブアセット Preamble.mrb（バイトコード）も生成し、ランタイムが起動時に一度評価する。
#
# 制約: cmd（特に入力待ちを挟む choose）を跨ぐとローカル変数が失われる（mruby Fiber の resume 挙動）。
# よって choose のキーはグローバルカウンタで採番し、cmd 直後にその場で読む。

def say(speaker, text = nil, display_as: nil)
  if text.nil?
    cmd :say, speaker_id: '', text: speaker
  else
    cmd :say, speaker_id: speaker.to_s, display_as: display_as, text: text
  end
end

def narration(text)
  cmd :say, speaker_id: '', text: text
end

def flag(key, value = 1)
  cmd :flag, key: key.to_s, value: value
end

def portrait(character, portrait_key)
  cmd :portrait, character: character.to_s, portrait_key: portrait_key.to_s
end

def bg(background_key)
  cmd :bg, background_key: background_key.to_s
end

def still(still_key)
  cmd :still, still_key: still_key.to_s
end

def se(se_key)
  cmd :se, se_key: se_key.to_s
end

def bgm(bgm_key = '')
  cmd :bgm, bgm_key: bgm_key.to_s
end

def wait(seconds)
  cmd :wait, seconds: seconds
end

# 選択肢提示 → 選んだ index を返す。キーはユニーク採番（state-model: 衝突回避）。
# グローバルはローカルと違い Fiber resume を跨いで残るため、cmd 後に同じキーで読み戻せる。
def choose(options)
  $__novel_choice_seq = ($__novel_choice_seq || 0) + 1
  cmd :choose, options: options, state_key: "__choice_#{$__novel_choice_seq}"
  state["__choice_#{$__novel_choice_seq}".to_sym]
end
