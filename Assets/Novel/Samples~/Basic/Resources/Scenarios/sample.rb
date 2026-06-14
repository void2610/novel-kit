chara :alice            # 登場キャラの分だけ糖衣を生やす
chara :bob
bg "room"
portrait :alice, "smile"
alice "Hi, welcome to <color=#8cf>novel-kit</color>."
alice "Text shows<w=0.4> bit<w=0.4> by bit. <shake>Surprised</shake>?"
bob "I'm Bob."
narration "-- She looked at you."
n = choose(["Yes", "No"])
flag "answered", 1
if n == 0
  alice "Glad to hear it!", as: "Alice (smiling)"
else
  alice "I see..."
end
narration "(answered=#{val(:answered)})"
narration "(The End)"
