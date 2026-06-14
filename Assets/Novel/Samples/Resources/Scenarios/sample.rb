bg "room"
portrait :alice, "smile"
say "alice", "Hi, welcome to <color=#8cf>novel-kit</color>."
say "alice", "Text shows<w=0.4> bit<w=0.4> by bit. <shake>Surprised</shake>?"
narration "-- She looked at you."
n = choose(["Yes", "No"])
flag "answered", 1
if n == 0
  say "alice", "Glad to hear it!"
else
  say "alice", "I see..."
end
narration "(The End)"
