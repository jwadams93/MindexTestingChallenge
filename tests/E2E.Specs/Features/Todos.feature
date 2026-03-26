Feature: Todos E2E

  Background:
    Given the app is running at "http://localhost:5173"
    And I reset data

# ── Existing Scenarios ──────────────────────────────────────

  Scenario: Add, complete, and delete a todo
    Given I open the unpopulated Todos page
    When I add a todo titled "Buy milk"
    Then I should see "Buy milk" in the list
    When I complete the todo "Buy milk"
    Then the todo "Buy milk" should appear completed
    When I delete the todo "Buy milk"
    Then I should not see "Buy milk" in the list

  @smoke @e2e
  Scenario: Add a todo (happy path)
    Given I open the unpopulated Todos page
    When I add a todo titled "Buy milk"
    Then I should see "Buy milk" in the list


# ── My Scenarios ────────────────────────────────────────────

# Suggested


  # Positive Scenarios

  # ── Create ────────────────────────────────────────────

  Scenario: Create a todo with priority and due date
    Given I open the unpopulated Todos page
    When I create a todo titled "Schedule dentist" with:
      | priority | dueDate |
      | High     | +3d     |
    Then I should see "Schedule dentist" in the list
    And I should see "Schedule dentist" with priority "High"
    And it should show a due date within 3 days

  Scenario: Create an overdue todo
    Given I seed todos:
      | title        | priority | dueDate |
      | Overdue task | High     | -3d     |
    And I open the Todos page
    Then I should see "Overdue task" with priority "High"
    And I should see "Overdue task" marked as overdue

  # ── Filter ────────────────────────────────────────────

  Scenario: Filter by priority: High and status: All
    Given I seed todos:
      | title        | priority | dueDate  |
      | do taxes     | High     | +21d     |
      | groceries    | Low      | +6d      |
      | hire jake    | High     | -6d      |
      | exercise     | Medium   | +4d      |
      | chores       | Low      | +9d      |
    And I open the Todos page
    When I set filter Priority to "High" and Status to "All" expecting "do taxes"
    Then I should see in the list:
      | title        |
      | do taxes     |
      | hire jake    |
  
  Scenario: Filter by priority: High and status: Complete 
    Given I seed todos:
      | title        | priority | dueDate  |
      | do taxes     | High     | +21d     |
      | groceries    | Low      | +6d      |
      | hire jake    | High     | -6d      |
      | exercise     | Medium   | +4d      |
      | chores       | Low      | +9d      |
    And I open the Todos page
    When I complete the todo "hire jake"
    And I set filter Priority to "High" and Status to "Complete" expecting "hire jake"
    Then I should see in the list:
      | title        |
      | hire jake    |

  Scenario: Filter by priority: Low and status: Active 
    Given I seed todos:
      | title        | priority | dueDate  |
      | do taxes     | Low      | +21d     |
      | groceries    | Low      | +6d      |
      | hire jake    | High     | -6d      |
      | exercise     | Low      | +4d      |
      | chores       | Low      | +9d      |
    And I open the Todos page
    When I complete the todo "do taxes"
    Then the todo "do taxes" should appear completed
    When I set filter Priority to "Low" and Status to "Active" expecting "exercise"
    Then I should see in the list:
      | title        |
      | exercise     |
      | chores       |
      | groceries    |

  # ── Search ────────────────────────────────────────────

  Scenario: Search All and sort by Title 
    Given I seed todos:
      | title              | priority | dueDate  |
      | cook dinner        | Low      | +21d     |
      | think about dinner | Low      | +6d      |
      | go to dinner       | High     | -6d      |
      | skip breakfast     | Low      | +4d      |
      | make lunch         | Low      | +9d      |
    And I open the Todos page
    When I sort by "Sort: Title"
    And I set filter Priority to "Low" and Status to "Active" expecting "cook dinner"
    Then I should see exactly:
      | title              |
      | cook dinner        |
      | make lunch         |
      | skip breakfast     |
      | think about dinner |
      
  Scenario: Search All and sort by Priority
    Given I seed todos:
      | title              | priority    | dueDate  |
      | cook dinner        | Low         | +21d     |
      | think about dinner | Low         | +6d      |
      | go to dinner       | High        | -6d      |
      | skip breakfast     | Medium      | +4d      |
      | make lunch         | Low         | +9d      |
    And I open the Todos page
    When I sort by "Sort: Priority"
    Then I should see exactly:
      | title              |
      | go to dinner       |
      | skip breakfast     |
      | cook dinner        |
      | make lunch         |
      | think about dinner |

  Scenario: Search All and sort by Due date 
    Given I seed todos:
      | title              | priority    | dueDate  |
      | cook dinner        | Low         | +21d     |
      | think about dinner | Low         | +6d      |
      | go to dinner       | High        | -6d      |
      | skip breakfast     | Medium      | +4d      |
      | make lunch         | Low         | +9d      |
    And I open the Todos page
    When I sort by "Sort: Due date"
    Then I should see exactly:
      | title              |
      | go to dinner       |
      | skip breakfast     |
      | think about dinner |
      | make lunch         |
      | cook dinner        |

  # ── Bulk Operations ────────────────────────────────────────────

  Scenario: Bulk Complete All 
    Given I seed todos:
      | title              | priority    | dueDate  |
      | cook dinner        | Low         | +21d     |
      | go to dinner       | High        | -6d      |
      | skip breakfast     | Medium      | +4d      |
    And I open the Todos page
    When I search for "dinner" and select all items
    And I apply bulk action "complete"
    Then both items should appear completed

  Scenario: Bulk Delete All
    Given I seed todos:
      | title              | priority    | dueDate  |
      | cook dinner        | Low         | +21d     |
      | go to dinner       | High        | -6d      |
      | skip breakfast     | Medium      | +4d      |
    And I open the Todos page
    When I search for "dinner" and select all items
    And I apply bulk action "delete"
    Then I should not see "cook dinner" in the list
    And I should not see "go to dinner" in the list

  Scenario: Bulk Complete Some
    Given I seed todos:
      | title              | priority    | dueDate  |
      | cook dinner        | Low         | +21d     |
      | go to dinner       | High        | -6d      |
      | skip breakfast     | Medium      | +4d      |
    And I open the Todos page
    When I select the following todos:
      | title         |
      | cook dinner   |
      | go to dinner  |
    And I apply bulk action "complete"
    Then "cook dinner" should appear completed
    And "go to dinner" should appear completed

  Scenario: Bulk Delete Some
    Given I seed todos:
      | title              | priority    | dueDate  |
      | cook dinner        | Low         | +21d     |
      | go to dinner       | High        | -6d      |
      | skip breakfast     | Medium      | +4d      |
    And I open the Todos page
    When I select the following todos:
      | title         |
      | cook dinner   |
      | go to dinner  |
    And I apply bulk action "delete"
    Then I should see exactly:
      | title         |
      | skip breakfast|



  # Negative Scenarios

  Scenario: Edit title results in duplicate todos error
    Given I seed todos:
      | title        | priority | tags    | dueDate  |
      | update test 1| Low      | inTest  | +10d     |
      | update test 2| Low      | inDev   | +10d     |
    And I open the Todos page
    When I edit "update test 2" to title "update test 1" 
    Then I should see an alert containing the error: "Duplicate title"