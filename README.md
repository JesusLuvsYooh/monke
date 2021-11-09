# Monke Encryption for Mirror
 
Monke is a plug and play encrypted transport layer for mirror.

Prevent hackers from spying on your monkey business! 


# What it does
Monke's a step towards an extra layer of security.  
Encrypts outgoing messages and decrypts incoming messages.  
Server sends its public key on connect -> client sends his -> everything else after that is encrypted.  
"if i sit outside your house and wireshark your internet, all the data from your game will be random garbage to me"

Currently Monke only encrypts everything, which isn't ideal, but can have humorous upsides.  
A quote from some Geniuses  
Genius1: "y would u want to encrypt player position"  
Genius2: "annoy people trying to write bots"  
Genius2: "its batched anyways so.. 16 bytes per 1kb message (for unreliable) is nothing really"  

# Notes
Monke is NOT a full fledged security solution for Mirror, and is best when combined with other obfuscation, authentication and anti tamper methods.  
The only thing that's unsecured is the initial hand shake.  
If you are using webgl with HTTPS, then the transport will already be encrypted and monke is not needed.  

# How to use

1. Add Monke.cs to your NetworkManager object.  
2. Drag Monke into the NetworkManager Transport field.  
3. Drag the transport you want to use (ie. KCP, Telepathy) into the 'Communication Transport' field in Monke.  

Screenshot demonstration:  
![UnityNM1](https://user-images.githubusercontent.com/57072365/140966716-6db91974-1db0-44c0-ac1e-fd13f13e0f2f.jpg)

Tick the "Show Debug Logs" bool, have a player join server/host, and you will see the following in console:  
![UnityGameView1](https://user-images.githubusercontent.com/57072365/140966906-d74117ca-b206-4d41-8d10-adacb2143665.jpg)

# Debugging

Console error:
Assets/MonkeTransport/Monke.cs(274,9): error CS0227: Unsafe code may only appear if compiling with /unsafe. Enable "Allow 'unsafe' code" in Player Settings to fix this error.
![UnityAllowUnsafe](https://user-images.githubusercontent.com/57072365/141006450-7c23b9b2-ce2d-4044-9658-f53aaf92b520.jpg)

Mac "libsodium.dylib" cannot be opened because the developer cannot be verified.
Go to System Preferences, Security and Privacy, and click "Allow Anyway".
![UnityDLL](https://user-images.githubusercontent.com/57072365/141006521-7ed4bbe5-8c55-474b-8b2b-3f5ad2011514.jpg)
