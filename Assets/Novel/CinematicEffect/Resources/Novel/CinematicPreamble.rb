# CinematicEffect 連携の糖衣 (Novel.CinematicEffect を RegisterNovelCinematicEffects で登録したときだけ評価される)。
# Resources/Novel/Effects/<key>.asset を置けば、その名前で呼べる。対応表は無い。
#
#   cinematic :vignette        # <key>.asset を再生 (Enter)
#   cinematic_stop :vignette   # <key>_exit.asset があればそれを再生。無ければ Enter が始めた演出を止める
#
# 画面揺れ・フェード等の標準 5 種 (shake / flash / fade_out / fade_in / blackout) は従来どおり world_effect 経由。
def cinematic(key)
  cmd :cinematic, key: key.to_s, stop: false
end

def cinematic_stop(key)
  cmd :cinematic, key: key.to_s, stop: true
end
