# Laura Parameter Compressor

## Features

### VRChat Parameter Compression
Due to some annoyances with other tools, I now made my own parameter compressor.

You can add it to your VCC here: https://laurarozier.github.io/LauraVRCUtils/  
In VCC you can add it by adding the package `Laura Parameter Compressor`

This one uses either 16 or 24 bits for the syncing, depending on whether or not you have bools or ints/floats to sync.
It syncs one int/float and 8 bools per step and transitions after half a second.
I tried to make it as fast as possible without breaking/impacting VRC. Let me know if syncing acts odd.

It will only consider bools "worthy" when there are more than 8, and won't so any compression if there are no bit savings.
It also creates backups of your VRC Parameter file and FX controller before making any changes.

This tool is "destructive", as in, it needs to be manually applied and makes changes to your parameter file and FX controller.

You can find the tool under `Tools` > `LauraRozier` > `Parameter Compressor`

#### Before Compression
![DocScreen1](https://github.com/user-attachments/assets/4733ae34-4274-41d5-a313-af98b6c565cb)
![DocScreen2](https://github.com/user-attachments/assets/cf5e713e-4cec-46d6-92e5-23ed6f0b93f7)

#### After Compression
![DocScreen3](https://github.com/user-attachments/assets/6ad00f64-7f0e-48d2-b48e-fc4d9f4fa303)
![DocScreen4](https://github.com/user-attachments/assets/f55d30aa-bb27-4edf-a8be-9c726b1a8b4e)
![DocScreen5](https://github.com/user-attachments/assets/43e74b0e-1def-406b-b9b3-1f62e08a2f16)
