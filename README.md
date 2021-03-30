# Dedofuscator
This is a deobfuscator for Ankama's Dofuscator that is used in the RawDataMessage**

It is still in a beta phase, which means it is not completed yet. as the weird functions that calculate some stuff are still there and it's still in progress.

USAGE:

- Decompile your RawDataMessage.swf using JPEX Decompiler, (IMPORTANT: Automatic deobfuscation should be ON ! otherwise this wont work.)
- Put your decompiled sources in the /Input folder, then run the program.
- Once you have the deobfuscated sources, download hurlant's crypto library sources from [here](https://crypto.hurlant.com/demo/srcview/Crypto.zip), and add everything to an adobe AIR project.

TODO: Fix garbage names for some classes in the hurlant lib.
TODO: Deobfuscate the weird functions that calculate the weird stuff

** RawDataMessage is a Message in Dofus 2 that sends an swf file to be executed in the client to verify it's integrity and such, it is obfuscated and a little bit hard to read, but this fixes this issue.
