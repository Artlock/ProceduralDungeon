Missing features that would have been added if more time was available:
- Force a start and end tileset for the main path
- Enable doors along the main path depending on the number of available keys up to that point in the path

Extra features that could have been added:
- Non linear secondary paths since ours are only linear with turns
(A method to get the deepest node of a secondary path is coded but unused - Would have been used for key placement in such cases)

Important Note:
Only rooms with 4 doors were designed to have content
But the generation only generates main path with secondary paths coming from it
And a main path node in this implementation only has one secondary path
Meaning the 4 doors rooms with content are never selected...
