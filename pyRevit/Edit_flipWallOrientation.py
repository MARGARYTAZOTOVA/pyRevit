__window__.Close()
from Autodesk.Revit.DB import Transaction, Wall

doc = __revit__.ActiveUIDocument.Document
selection = [ doc.GetElement( elId ) for elId in __revit__.ActiveUIDocument.Selection.GetElementIds() ]

t = Transaction( doc, 'Flip Selected Walls')
t.Start()

for el in selection:
	if isinstance( el, Wall ):
		el.Flip()

t.Commit()