### Wabbajack Content Aware Diffing

One of the key problems with any generic algorithm is that it tends to work "okay"
 for the broad use cases, but poorly in specific cases. Octodiff will diff any file we throw at it, but it creates extremely large diffs when comparing
 two textures with the same content but differing compression formats. This library attempts to abstract away the binary diffing from the rest of the 
 Wabbajack application and allows us to setup different diffing algorithms based on file types. 
 
 Note the output hash of a diffed file is not guarenteed to be exactly the same as the source file, but is expected to be "reasonably the same". In other
 words, the output of patching a archive might be slightly different, but the contents of the archive are expected to be the same.
 
 #### Examples: 
 
 ##### DDS Compression
 If a uncomressed and a BC7 compressed image are compared, then the patch should attempt to perform BC7 compression on the input file first and perhaps
 also generate mipmaps before attempting to octodiff the result. In some cases (since BC7 is mostly lossess) this may result in a binary exact result with
 a OctoDiff patch size of 0
 
 ##### Archive Diffing
 If a .7z archive and a .zip archive are compared, both files should be examined, each file diffed, and a minimal patch created between the two. The result
 may be a .zip or a .7z, but the contents of the archives should be exactly the same.  