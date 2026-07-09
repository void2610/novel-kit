# novel-kit 共通 Ruby ヘルパ。作家が say/choose 等の素直な関数でシナリオを書けるようにする。
# 実体は VitalRouter.MRuby の cmd 発行で、ヘルパ名は NovelScenarioRunner の AddCommand 登録名に対応する。
# RubyScriptedImporter がサブアセット Preamble.mrb（バイトコード）も生成し、ランタイムが起動時に一度評価する。
#
# 制約: cmd（特に入力待ちを挟む choose）を跨ぐとローカル変数が失われる（mruby Fiber の resume 挙動）。
# よって choose のキーはグローバルカウンタで採番し、cmd 直後にその場で読む。

# 第3引数 portrait_key を渡すと、この行と同時に立ち絵を切り替えられる（表示名 display_as と立ち絵を独立制御）。
# 例: say 'kii', '正体は伏せたまま', 'kii/default', display_as: '？？？'
def say(speaker, text = nil, portrait_key = nil, display_as: nil)
  if text.nil?
    cmd :say, speaker_id: '', text: speaker
  else
    cmd :say, speaker_id: speaker.to_s, display_as: display_as, text: text, portrait_key: portrait_key.to_s
  end
end

def narration(text)
  cmd :say, speaker_id: '', text: text
end

# キャラ名コマンド糖衣（command-schema ADR）を生やす。登場キャラの分だけ chara :alice と書けば、
# 以降 alice "セリフ" が say "alice", "セリフ" の糖衣になり、alice "…", as: "？？？" で表示名も上書きできる。
# プロジェクトごとのキャラ差はこの糖衣層で吸収する。MRubyCS は method_missing 未対応のため、
# define_method で実メソッドを定義する（インタプリタ機能でネイティブコンパイラ不要・移植性を保てる）。
def chara(id)
  id = id.to_s
  Object.class_eval do
    define_method(id) do |text, as: nil|
      say id, text, display_as: as
    end
  end
end

def flag(key, value = 1)
  cmd :flag, key: key.to_s, value: value
end

# 変数 read 糖衣（flag の読み出し側）。state[:key] のショートカット。未設定は 0 扱い。
def val(key)
  state[key.to_sym] || 0
end

# 真偽判定（0 以外を真とみなす）。例: `if flag?(:answered)`
def flag?(key)
  val(key) != 0
end

def portrait(character, portrait_key)
  cmd :portrait, character: character.to_s, portrait_key: portrait_key.to_s
end

# 場面 (stage) の cast を宣言。 layout id (:single / :pair / :trio / :quad / :penta / カスタム) と
# 「キャラ → slot index」 の対応を渡す。 配列形式 (順番で 0..N-1 を割り当て) と hash 形式 (明示) の両方を許容する。
# 例: stage :trio, [:taylor, :kii, :protagonist]
# 例: stage :trio, taylor: 1, kii: 0, protagonist: 2
# 第 2 引数を省略すると cast 無し (画面クリアに近い: layout だけ切替)。
def stage(layout_id, cast = nil)
  pairs = []
  if cast.is_a?(Hash)
    cast.each { |k, v| pairs << k.to_s; pairs << v.to_i.to_s }
  elsif cast.is_a?(Array)
    cast.each_with_index { |k, i| pairs << k.to_s; pairs << i.to_s }
  end
  cmd :stage, layout_id: layout_id.to_s, cast_pairs: pairs
end

# 指定キャラを場面から退場 (cast から外し、 該当 slot を非表示に)。 退場アニメは View 実装側で。
# Ruby 本体の Kernel#exit と被るので exit_chara にしている。
def exit_chara(character)
  cmd :exit, character: character.to_s
end

# すべての cast をクリアして場面をリセット (シーン切替時など)。 layout はリセットせず維持される。
def clear_stage
  cmd :clear_stage
end

def bg(background_key)
  cmd :bg, background_key: background_key.to_s
end

def still(still_key)
  cmd :still, still_key: still_key.to_s
end

def image(image_key)
  cmd :center_image, image_key: image_key.to_s
end

def hide_image
  cmd :hide_center_image
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

# 世界エフェクト（カメラ/画面/gameplay への脱出）。blocking 性は game の sink が決める
# （即完了タスク=非ブロッキング / 完了時解決タスク=ブロッキングで次行が待つ。effect-await）。
# 注: テキスト内の <shake> は文字演出で別物。こちらはカメラ等ゲーム本体への作用。
def world_effect(key, *args)
  # MRubyCS が Symbol#to_proc (&:to_f) を未サポートのため明示ブロックで変換する。
  cmd :world_effect, effect_key: key.to_s, args: args.map { |a| a.to_f }
end

def shake(intensity = 1.0)
  world_effect :shake, intensity
end

def flash(duration = 0.2)
  world_effect :flash, duration
end

def fade_out(duration = 1.0)
  world_effect :fade_out, duration
end

def fade_in(duration = 1.0)
  world_effect :fade_in, duration
end

def blackout(duration = 0.0)
  world_effect :blackout, duration
end

# テキストウィンドウを一時的に隠す / 戻す (イベント CG 全画面表示中など)
def hide_message_window
  cmd :message_window_visible, visible: false
end

def show_message_window
  cmd :message_window_visible, visible: true
end

# 選択肢提示 → 選んだ index を返す。
# 既定キーは `__` 始まりのユニーク採番で、一時スクラッチ（セーブに残さない）。
# 跨シナリオで選択結果を残したいときは key: を渡して `__` 以外の安定キーに書く（改稿耐性・セーブ対象）。
# 例: n = choose(["はい","いいえ"], key: :ask_truth)
# グローバルはローカルと違い Fiber resume を跨いで残るため、cmd 後に同じキーで読み戻せる。
def choose(options, key: nil)
  # キーはグローバルに保持する。ローカル変数は cmd（choose の入力待ち）の resume を跨ぐと失われるため、
  # cmd 後に読み戻すキーをローカルに置いてはいけない（グローバルは resume を跨いで残る）。
  if key.nil?
    $__novel_choice_seq = ($__novel_choice_seq || 0) + 1
    $__novel_choice_key = "__choice_#{$__novel_choice_seq}"
  else
    $__novel_choice_key = key.to_s
  end
  cmd :choose, options: options, state_key: $__novel_choice_key
  state[$__novel_choice_key.to_sym]
end
