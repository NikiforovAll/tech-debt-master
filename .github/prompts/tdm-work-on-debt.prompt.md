---
mode: agent
tools: ['changes', 'codebase', 'editFiles', 'fetch', 'findTestFiles', 'problems', 'runCommands', 'runTasks', 'search', 'searchResults', 'terminalLastCommand', 'terminalSelection', 'testFailure', 'usages', 'tdm-get-item', 'tdm-list-items', 'tdm-remove-item', 'tdm-show-repo-stats']
description: 'An autonomous workflow for identifying, analyzing, and resolving technical debt in a codebase to improve maintainability and efficiency.'
---

Execute the following workflow to systematically address technical debt:

1. Assessment Phase:
    - Use 'tdm-show-repo-stats' to gather repository-wide technical debt metrics
    - Review debt distribution across files, types, and severity levels
    - Document initial findings for reference

2. Prioritization Phase:
    - Use 'tdm-list-items' to retrieve first page of technical debt items (they are already sorted by priority by default)
    - Present a user with an items as markdown table
    - Don't use `tdm-get-item` yet

3. Resolution Phase (ONE BY ONE):
    - Use 'tdm-get-item' to fetch detailed item information
    - Present user with the item
    - Analyze item validity:
        - Review related code
        - Verify if debt is still relevant
        - Document investigation findings
    - For each valid item:
        - Implement necessary fixes
    - Remove resolved items using 'tdm-remove-item'
    - Complete ALL debt items in current file before proceeding

4. Validation Requirements:
    - Ensure all changes maintain existing functionality
    - Document any architectural decisions
    - Request human review for complex changes

### Constraints:

Request clarification when:
- Item description is ambiguous
- Multiple resolution approaches exist
- Implementation impact is unclear
- Dependencies affect other components

Use emojis where appropriate:
- ✅ for completed tasks
- ❗ for issues or blockers
- 📄 for documentation updates

- Once item is resolved or if it is not relevant anymore, remove it from the list using 'tdm-remove-item'. 
- Ask user for confirmation before removing.
- Ask user before starting the next item.