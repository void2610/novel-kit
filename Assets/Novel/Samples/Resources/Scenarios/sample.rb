bg "room"
portrait :alice, "smile"
say "alice", "やあ、ようこそ <color=#8cf>novel-kit</color> へ。"
say "alice", "文字は<w=0.4>少し<w=0.4>ずつ出るよ。<shake>びっくり</shake>した？"
narration "——彼女はこちらを見た。"
n = choose(["はい", "いいえ"])
flag "answered", 1
if n == 0
  say "alice", "うれしい！"
else
  say "alice", "そっか……。"
end
narration "（おわり）"
