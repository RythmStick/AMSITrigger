# AMSITrigger v3
## Hunting for Malicious Strings

### Usage:

    -i, -inputfile=VALUE       Powershell filename
    -u, -url=VALUE             URL eg. https://10.1.1.1/Invoke-NinjaCopy.ps1
    -f, -format=VALUE          Output Format:
                                  1 - Only show Triggers
                                  2 - Show Triggers with Line numbers
                                  3 - Show Triggers inline with code
                                  4 - Show AMSI calls (xmas tree mode)
    -d, -debug                 Show Debug Info
    -p, -pause=VALUE           Pause after displaying VALUE triggers  
    -m, -maxsiglength=VALUE    Maximum signature Length to cater for,
                                  default=2048
    -c, -chunksize=VALUE       Chunk size to send to AMSIScanBuffer,
                                  default=4096
    -h, -?, -help              Show Help
  
    
For details see https://www.rythmstick.net/posts/amsitrigger

